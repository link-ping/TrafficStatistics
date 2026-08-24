using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;

namespace TrafficStatistics.Core.Services;

/// <summary>
/// Captures per-process network traffic using ETW (Event Tracing for Windows)
/// via the Microsoft.Diagnostics.Tracing.TraceEvent library.
/// Requires administrative privileges.
/// </summary>
public sealed class EtwTrafficCaptureService : ITrafficCaptureService, IDisposable
{
    private const string SessionName = "TrafficStatistics-Kernel";

    private readonly ILogger<EtwTrafficCaptureService> _logger;
    private TraceEventSession? _session;
    private Thread? _processingThread;
    private volatile bool _stopping;

    /// <inheritdoc />
    public event Action<int, int, bool>? OnTrafficEvent;

    /// <summary>
    /// Initializes a new instance of the <see cref="EtwTrafficCaptureService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public EtwTrafficCaptureService(ILogger<EtwTrafficCaptureService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken ct)
    {
        _stopping = false;

        // Dispose any leftover session with the same name to avoid conflicts.
        DisposeExistingSession();

        _session = new TraceEventSession(SessionName)
        {
            StopOnDispose = true
        };

        _session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

        SubscribeToEvents(_session.Source);

        _processingThread = new Thread(() =>
        {
            try
            {
                _session.Source.Process();
            }
            catch (Exception ex) when (!_stopping)
            {
                _logger.LogError(ex, "ETW session processing failed");
            }
        })
        {
            Name = "ETW-NetworkCapture",
            IsBackground = true
        };

        _processingThread.Start();
        _logger.LogInformation("ETW traffic capture started (session: {SessionName})", SessionName);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken ct)
    {
        _stopping = true;

        try
        {
            _session?.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping ETW session");
        }

        _processingThread?.Join(TimeSpan.FromSeconds(5));
        _logger.LogInformation("ETW traffic capture stopped");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping = true;

        try
        {
            _session?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing ETW session");
        }

        _session = null;
    }

    private void SubscribeToEvents(ETWTraceEventSource source)
    {
        var parser = source.Kernel;

        // TCP IPv4
        parser.TcpIpSend += e => FireEvent(e.ProcessID, e.size, isSend: true);
        parser.TcpIpRecv += e => FireEvent(e.ProcessID, e.size, isSend: false);

        // TCP IPv6
        parser.TcpIpSendIPV6 += e => FireEvent(e.ProcessID, e.size, isSend: true);
        parser.TcpIpRecvIPV6 += e => FireEvent(e.ProcessID, e.size, isSend: false);

        // UDP IPv4
        parser.UdpIpSend += e => FireEvent(e.ProcessID, e.size, isSend: true);
        parser.UdpIpRecv += e => FireEvent(e.ProcessID, e.size, isSend: false);

        // UDP IPv6
        parser.UdpIpSendIPV6 += e => FireEvent(e.ProcessID, e.size, isSend: true);
        parser.UdpIpRecvIPV6 += e => FireEvent(e.ProcessID, e.size, isSend: false);
    }

    private void FireEvent(int pid, int bytes, bool isSend)
    {
        if (bytes > 0)
        {
            OnTrafficEvent?.Invoke(pid, bytes, isSend);
        }
    }

    private void DisposeExistingSession()
    {
        try
        {
            // GetActiveSessionNames returns currently active ETW sessions.
            // If our session name already exists, dispose it to avoid
            // "session already exists" errors.
            var activeNames = TraceEventSession.GetActiveSessionNames();
            if (activeNames.Contains(SessionName))
            {
                _logger.LogWarning("Found existing ETW session '{SessionName}', disposing it", SessionName);
                using var existing = new TraceEventSession(SessionName, TraceEventSessionOptions.Attach);
                existing.Stop();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispose existing ETW session '{SessionName}'", SessionName);
        }
    }
}

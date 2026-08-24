using System.Collections.Concurrent;
using TrafficStatistics.Core.Models;

namespace TrafficStatistics.Core.Services;

/// <summary>
/// Aggregates raw traffic events into per-process snapshots with per-second speed
/// calculations and per-minute flush events for persistence.
/// </summary>
public sealed class AggregationService : IAggregationService, IDisposable
{
    private readonly ConcurrentDictionary<int, TrafficAccumulator> _accumulators = new();
    private readonly ProcessInfoService _processInfoService;
    private readonly Timer _secondTimer;
    private readonly Timer _minuteTimer;
    private readonly object _snapshotLock = new();

    private IReadOnlyDictionary<int, TrafficSnapshot> _currentSnapshots =
        new Dictionary<int, TrafficSnapshot>();

    private long _totalUploadSpeed;
    private long _totalDownloadSpeed;

    /// <inheritdoc />
    public event Action? OnSnapshotsUpdated;

    /// <inheritdoc />
    public event Action<Dictionary<int, (long sent, long recv)>>? OnMinuteFlush;

    /// <inheritdoc />
    public long TotalUploadSpeed => Interlocked.Read(ref _totalUploadSpeed);

    /// <inheritdoc />
    public long TotalDownloadSpeed => Interlocked.Read(ref _totalDownloadSpeed);

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregationService"/> class.
    /// </summary>
    /// <param name="processInfoService">Service to resolve PIDs to process info.</param>
    public AggregationService(ProcessInfoService processInfoService)
    {
        _processInfoService = processInfoService;

        // Fire every 1 second for speed calculation and snapshot update.
        _secondTimer = new Timer(OnSecondTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        // Fire every 60 seconds for minute-level persistence flush.
        _minuteTimer = new Timer(OnMinuteTick, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<int, TrafficSnapshot> GetCurrentSnapshots()
    {
        lock (_snapshotLock)
        {
            return _currentSnapshots;
        }
    }

    /// <inheritdoc />
    public void RecordTraffic(int pid, int bytes, bool isSend)
    {
        var accumulator = _accumulators.GetOrAdd(pid, _ => new TrafficAccumulator());

        if (isSend)
        {
            Interlocked.Add(ref accumulator.CurrentSecondSent, bytes);
            Interlocked.Add(ref accumulator.TotalSent, bytes);
            Interlocked.Add(ref accumulator.MinuteSent, bytes);
        }
        else
        {
            Interlocked.Add(ref accumulator.CurrentSecondRecv, bytes);
            Interlocked.Add(ref accumulator.TotalRecv, bytes);
            Interlocked.Add(ref accumulator.MinuteRecv, bytes);
        }

        accumulator.LastActive = DateTime.UtcNow;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _secondTimer.Dispose();
        _minuteTimer.Dispose();
    }

    private void OnSecondTick(object? state)
    {
        var snapshots = new Dictionary<int, TrafficSnapshot>();
        long totalUp = 0;
        long totalDown = 0;

        foreach (var (pid, acc) in _accumulators)
        {
            long sent = Interlocked.Exchange(ref acc.CurrentSecondSent, 0);
            long recv = Interlocked.Exchange(ref acc.CurrentSecondRecv, 0);

            var processInfo = _processInfoService.GetProcessInfo(pid);

            var snapshot = new TrafficSnapshot
            {
                Pid = pid,
                ProcessName = processInfo?.Name ?? $"PID {pid}",
                ProcessPath = processInfo?.Path,
                UploadSpeed = sent,
                DownloadSpeed = recv,
                TotalSent = Interlocked.Read(ref acc.TotalSent),
                TotalRecv = Interlocked.Read(ref acc.TotalRecv),
                LastActive = acc.LastActive
            };

            snapshots[pid] = snapshot;
            totalUp += sent;
            totalDown += recv;
        }

        Interlocked.Exchange(ref _totalUploadSpeed, totalUp);
        Interlocked.Exchange(ref _totalDownloadSpeed, totalDown);

        lock (_snapshotLock)
        {
            _currentSnapshots = snapshots;
        }

        OnSnapshotsUpdated?.Invoke();
    }

    private void OnMinuteTick(object? state)
    {
        var flushData = new Dictionary<int, (long sent, long recv)>();

        foreach (var (pid, acc) in _accumulators)
        {
            long sent = Interlocked.Exchange(ref acc.MinuteSent, 0);
            long recv = Interlocked.Exchange(ref acc.MinuteRecv, 0);

            if (sent > 0 || recv > 0)
            {
                flushData[pid] = (sent, recv);
            }
        }

        if (flushData.Count > 0)
        {
            OnMinuteFlush?.Invoke(flushData);
        }
    }

    /// <summary>
    /// Internal accumulator for tracking traffic counters per process.
    /// All fields are modified via <see cref="Interlocked"/> operations.
    /// </summary>
    private sealed class TrafficAccumulator
    {
        /// <summary>Bytes sent in the current 1-second window (for speed calculation).</summary>
        public long CurrentSecondSent;

        /// <summary>Bytes received in the current 1-second window (for speed calculation).</summary>
        public long CurrentSecondRecv;

        /// <summary>Total bytes sent since application start.</summary>
        public long TotalSent;

        /// <summary>Total bytes received since application start.</summary>
        public long TotalRecv;

        /// <summary>Bytes sent in the current minute window (for persistence flush).</summary>
        public long MinuteSent;

        /// <summary>Bytes received in the current minute window (for persistence flush).</summary>
        public long MinuteRecv;

        /// <summary>Timestamp of the last network activity.</summary>
        public DateTime LastActive;
    }
}

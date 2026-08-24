namespace TrafficStatistics.Core.Services;

/// <summary>
/// Captures per-process network traffic events from the operating system.
/// </summary>
public interface ITrafficCaptureService
{
    /// <summary>
    /// Starts capturing network traffic events.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the capture.</param>
    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Stops capturing network traffic events.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task StopAsync(CancellationToken ct);

    /// <summary>
    /// Raised for each observed traffic event.
    /// Parameters: process ID, byte count, whether it is a send (true) or receive (false).
    /// </summary>
    event Action<int, int, bool>? OnTrafficEvent;
}

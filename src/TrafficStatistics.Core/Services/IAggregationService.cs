using TrafficStatistics.Core.Models;

namespace TrafficStatistics.Core.Services;

/// <summary>
/// Aggregates raw traffic events into per-process snapshots with speed calculations.
/// </summary>
public interface IAggregationService
{
    /// <summary>
    /// Returns the current set of traffic snapshots keyed by PID.
    /// </summary>
    IReadOnlyDictionary<int, TrafficSnapshot> GetCurrentSnapshots();

    /// <summary>
    /// Records a traffic event for the given process.
    /// This method must be thread-safe.
    /// </summary>
    /// <param name="pid">Process identifier.</param>
    /// <param name="bytes">Number of bytes transferred.</param>
    /// <param name="isSend">True if the traffic is outbound, false if inbound.</param>
    void RecordTraffic(int pid, int bytes, bool isSend);

    /// <summary>
    /// Fired approximately every second after snapshots are recalculated.
    /// </summary>
    event Action? OnSnapshotsUpdated;

    /// <summary>
    /// Fired every minute with accumulated (sent, recv) bytes per PID for persistence.
    /// </summary>
    event Action<Dictionary<int, (long sent, long recv)>>? OnMinuteFlush;

    /// <summary>
    /// Total upload speed across all processes in bytes per second.
    /// </summary>
    long TotalUploadSpeed { get; }

    /// <summary>
    /// Total download speed across all processes in bytes per second.
    /// </summary>
    long TotalDownloadSpeed { get; }
}

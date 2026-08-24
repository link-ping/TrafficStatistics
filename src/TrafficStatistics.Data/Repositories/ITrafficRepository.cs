using TrafficStatistics.Core.Models;

namespace TrafficStatistics.Data.Repositories;

/// <summary>
/// Defines data access operations for traffic monitoring persistence.
/// </summary>
public interface ITrafficRepository
{
    /// <summary>
    /// Retrieves an existing process by name and path, or creates a new one if it does not exist.
    /// </summary>
    /// <param name="name">The process name (e.g. "chrome.exe").</param>
    /// <param name="path">The full path to the executable, or <c>null</c> if unknown.</param>
    /// <returns>The existing or newly created <see cref="ProcessEntity"/>.</returns>
    Task<ProcessEntity> GetOrCreateProcessAsync(string name, string? path);

    /// <summary>
    /// Persists a batch of traffic records using an efficient upsert (INSERT OR REPLACE) strategy.
    /// </summary>
    /// <param name="records">
    /// A collection of tuples containing process ID, Unix timestamp, bytes sent, and bytes received.
    /// </param>
    Task SaveTrafficRecordsAsync(IEnumerable<(int processId, long timestamp, long bytesSent, long bytesRecv)> records);

    /// <summary>
    /// Queries traffic records within a timestamp range, optionally filtered by process.
    /// </summary>
    /// <param name="processId">If specified, limits results to a single process.</param>
    /// <param name="startTimestamp">Inclusive lower bound Unix timestamp.</param>
    /// <param name="endTimestamp">Inclusive upper bound Unix timestamp.</param>
    /// <returns>A read-only list of matching traffic records.</returns>
    Task<IReadOnlyList<TrafficRecord>> GetTrafficAsync(int? processId, long startTimestamp, long endTimestamp);

    /// <summary>
    /// Queries daily summaries within a date range, optionally filtered by process.
    /// </summary>
    /// <param name="processId">If specified, limits results to a single process.</param>
    /// <param name="startDate">Inclusive start date in "yyyy-MM-dd" format.</param>
    /// <param name="endDate">Inclusive end date in "yyyy-MM-dd" format.</param>
    /// <returns>A read-only list of matching daily summaries.</returns>
    Task<IReadOnlyList<DailySummary>> GetDailySummariesAsync(int? processId, string startDate, string endDate);

    /// <summary>
    /// Aggregates all traffic records for the given date into the daily summaries table.
    /// Existing summaries for the date are upserted.
    /// </summary>
    /// <param name="date">The date to aggregate in "yyyy-MM-dd" format.</param>
    Task AggregateDailySummaryAsync(string date);

    /// <summary>
    /// Deletes traffic records and daily summaries older than the specified retention periods.
    /// </summary>
    /// <param name="minuteDataRetentionDays">Number of days to retain minute-granularity traffic records.</param>
    /// <param name="dailyDataRetentionDays">Number of days to retain daily summary records.</param>
    Task PurgeOldDataAsync(int minuteDataRetentionDays, int dailyDataRetentionDays);

    /// <summary>
    /// Retrieves all known processes from the database.
    /// </summary>
    /// <returns>A read-only list of all process entities.</returns>
    Task<IReadOnlyList<ProcessEntity>> GetAllProcessesAsync();

    /// <summary>
    /// Returns the top N processes by total traffic volume within a timestamp range.
    /// </summary>
    /// <param name="startTimestamp">Inclusive lower bound Unix timestamp.</param>
    /// <param name="endTimestamp">Inclusive upper bound Unix timestamp.</param>
    /// <param name="topN">Maximum number of processes to return (default 10).</param>
    /// <returns>
    /// A dictionary mapping process IDs to a tuple of (totalSent, totalRecv),
    /// ordered by descending total traffic.
    /// </returns>
    Task<Dictionary<int, (long totalSent, long totalRecv)>> GetTopProcessesAsync(long startTimestamp, long endTimestamp, int topN = 10);
}

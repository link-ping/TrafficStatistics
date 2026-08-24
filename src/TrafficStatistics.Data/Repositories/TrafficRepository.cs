using System.Text;
using Microsoft.EntityFrameworkCore;
using TrafficStatistics.Core.Models;

namespace TrafficStatistics.Data.Repositories;

/// <summary>
/// SQLite-backed implementation of <see cref="ITrafficRepository"/>
/// using Entity Framework Core and raw SQL for performance-critical operations.
/// </summary>
public class TrafficRepository : ITrafficRepository
{
    private readonly TrafficDbContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="TrafficRepository"/>.
    /// </summary>
    /// <param name="context">The EF Core database context.</param>
    public TrafficRepository(TrafficDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ProcessEntity> GetOrCreateProcessAsync(string name, string? path)
    {
        var existing = await _context.Processes
            .FirstOrDefaultAsync(p => p.Name == name && p.Path == path);

        if (existing is not null)
        {
            return existing;
        }

        var entity = new ProcessEntity { Name = name, Path = path };
        _context.Processes.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    /// <inheritdoc />
    public async Task SaveTrafficRecordsAsync(
        IEnumerable<(int processId, long timestamp, long bytesSent, long bytesRecv)> records)
    {
        var recordList = records as IList<(int processId, long timestamp, long bytesSent, long bytesRecv)>
            ?? records.ToList();

        if (recordList.Count == 0)
        {
            return;
        }

        // Batch INSERT OR REPLACE for performance.
        // SQLite supports multi-row VALUES in a single statement.
        const int batchSize = 500;

        for (int i = 0; i < recordList.Count; i += batchSize)
        {
            var batch = recordList.Skip(i).Take(batchSize).ToList();
            var sql = new StringBuilder();
            sql.Append("INSERT OR REPLACE INTO TrafficRecords (ProcessId, Timestamp, BytesSent, BytesRecv) VALUES ");

            var parameters = new List<object>();
            for (int j = 0; j < batch.Count; j++)
            {
                if (j > 0)
                {
                    sql.Append(", ");
                }

                int paramBase = j * 4;
                sql.Append($"({{{paramBase}}}, {{{paramBase + 1}}}, {{{paramBase + 2}}}, {{{paramBase + 3}}})");
                parameters.Add(batch[j].processId);
                parameters.Add(batch[j].timestamp);
                parameters.Add(batch[j].bytesSent);
                parameters.Add(batch[j].bytesRecv);
            }

            await _context.Database.ExecuteSqlRawAsync(sql.ToString(), parameters.ToArray());
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrafficRecord>> GetTrafficAsync(
        int? processId, long startTimestamp, long endTimestamp)
    {
        var query = _context.TrafficRecords
            .Where(r => r.Timestamp >= startTimestamp && r.Timestamp <= endTimestamp);

        if (processId.HasValue)
        {
            query = query.Where(r => r.ProcessId == processId.Value);
        }

        return await query.OrderBy(r => r.Timestamp).ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DailySummary>> GetDailySummariesAsync(
        int? processId, string startDate, string endDate)
    {
        var query = _context.DailySummaries
            .Where(d => string.Compare(d.Date, startDate) >= 0 && string.Compare(d.Date, endDate) <= 0);

        if (processId.HasValue)
        {
            query = query.Where(d => d.ProcessId == processId.Value);
        }

        return await query.OrderBy(d => d.Date).ToListAsync();
    }

    /// <inheritdoc />
    public async Task AggregateDailySummaryAsync(string date)
    {
        // Calculate the Unix timestamp range for the given date (UTC).
        var parsedDate = DateTime.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var startTimestamp = new DateTimeOffset(parsedDate, TimeSpan.Zero).ToUnixTimeSeconds();
        var endTimestamp = new DateTimeOffset(parsedDate.AddDays(1), TimeSpan.Zero).ToUnixTimeSeconds() - 1;

        // Upsert aggregated data into DailySummaries.
        // Uses INSERT ... ON CONFLICT on the unique (ProcessId, Date) index to update existing rows.
        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO DailySummaries (ProcessId, Date, TotalSent, TotalRecv)
            SELECT ProcessId, {0}, SUM(BytesSent), SUM(BytesRecv)
            FROM TrafficRecords
            WHERE Timestamp >= {1} AND Timestamp <= {2}
            GROUP BY ProcessId
            ON CONFLICT (ProcessId, Date) DO UPDATE SET
                TotalSent = excluded.TotalSent,
                TotalRecv = excluded.TotalRecv
            """,
            date, startTimestamp, endTimestamp);
    }

    /// <inheritdoc />
    public async Task PurgeOldDataAsync(int minuteDataRetentionDays, int dailyDataRetentionDays)
    {
        var now = DateTimeOffset.UtcNow;

        // Purge minute-granularity traffic records.
        var minuteCutoff = now.AddDays(-minuteDataRetentionDays).ToUnixTimeSeconds();
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM TrafficRecords WHERE Timestamp < {0}",
            minuteCutoff);

        // Purge daily summaries.
        var dailyCutoff = now.AddDays(-dailyDataRetentionDays).ToString("yyyy-MM-dd");
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM DailySummaries WHERE Date < {0}",
            dailyCutoff);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessEntity>> GetAllProcessesAsync()
    {
        return await _context.Processes.OrderBy(p => p.Name).ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, (long totalSent, long totalRecv)>> GetTopProcessesAsync(
        long startTimestamp, long endTimestamp, int topN = 10)
    {
        var results = await _context.TrafficRecords
            .Where(r => r.Timestamp >= startTimestamp && r.Timestamp <= endTimestamp)
            .GroupBy(r => r.ProcessId)
            .Select(g => new
            {
                ProcessId = g.Key,
                TotalSent = g.Sum(r => r.BytesSent),
                TotalRecv = g.Sum(r => r.BytesRecv)
            })
            .OrderByDescending(x => x.TotalSent + x.TotalRecv)
            .Take(topN)
            .ToListAsync();

        var dict = new Dictionary<int, (long totalSent, long totalRecv)>();
        foreach (var r in results)
        {
            dict[r.ProcessId] = (r.TotalSent, r.TotalRecv);
        }

        return dict;
    }
}

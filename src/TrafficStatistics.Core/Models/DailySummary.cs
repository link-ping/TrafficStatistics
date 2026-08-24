namespace TrafficStatistics.Core.Models;

/// <summary>
/// Entity representing daily aggregated traffic data for a process.
/// </summary>
public sealed class DailySummary
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Foreign key referencing <see cref="ProcessEntity.Id"/>.</summary>
    public int ProcessId { get; set; }

    /// <summary>Date string in "yyyy-MM-dd" format.</summary>
    public required string Date { get; set; }

    /// <summary>Total bytes sent on this date.</summary>
    public long TotalSent { get; set; }

    /// <summary>Total bytes received on this date.</summary>
    public long TotalRecv { get; set; }
}

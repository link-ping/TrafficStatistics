namespace TrafficStatistics.Core.Models;

/// <summary>
/// Entity representing a minute-granularity traffic record persisted to the database.
/// </summary>
public sealed class TrafficRecord
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Foreign key referencing <see cref="ProcessEntity.Id"/>.</summary>
    public int ProcessId { get; set; }

    /// <summary>Unix timestamp aligned to the start of the minute.</summary>
    public long Timestamp { get; set; }

    /// <summary>Total bytes sent during this minute.</summary>
    public long BytesSent { get; set; }

    /// <summary>Total bytes received during this minute.</summary>
    public long BytesRecv { get; set; }
}

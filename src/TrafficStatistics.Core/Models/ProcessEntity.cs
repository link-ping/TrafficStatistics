namespace TrafficStatistics.Core.Models;

/// <summary>
/// Entity representing a known process persisted to the Processes table.
/// A UNIQUE constraint on (<see cref="Name"/>, <see cref="Path"/>) should be
/// enforced at the database level to prevent duplicate entries.
/// </summary>
public sealed class ProcessEntity
{
    /// <summary>Primary key, auto-incremented.</summary>
    public int Id { get; set; }

    /// <summary>Process name (e.g. "chrome.exe").</summary>
    public required string Name { get; set; }

    /// <summary>Full path to the executable, if available.</summary>
    public string? Path { get; set; }
}

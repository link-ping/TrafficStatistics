namespace TrafficStatistics.Core.Models;

/// <summary>
/// Real-time traffic snapshot for a single process.
/// This is a view-model-friendly class used for live speed display and is NOT persisted.
/// </summary>
public sealed class TrafficSnapshot
{
    /// <summary>Operating system process identifier.</summary>
    public int Pid { get; set; }

    /// <summary>Process name (e.g. "chrome.exe").</summary>
    public required string ProcessName { get; set; }

    /// <summary>Full path to the process executable, if available.</summary>
    public string? ProcessPath { get; set; }

    /// <summary>Current upload speed in bytes per second.</summary>
    public long UploadSpeed { get; set; }

    /// <summary>Current download speed in bytes per second.</summary>
    public long DownloadSpeed { get; set; }

    /// <summary>Total bytes sent since application start (or today).</summary>
    public long TotalSent { get; set; }

    /// <summary>Total bytes received since application start (or today).</summary>
    public long TotalRecv { get; set; }

    /// <summary>Timestamp of the last observed network activity for this process.</summary>
    public DateTime LastActive { get; set; }
}

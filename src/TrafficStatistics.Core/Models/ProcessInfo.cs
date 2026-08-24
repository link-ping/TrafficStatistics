namespace TrafficStatistics.Core.Models;

/// <summary>
/// Represents a running process identified by PID.
/// Icon is not stored here; it is retrieved lazily in the UI layer.
/// </summary>
/// <param name="Pid">The process identifier.</param>
/// <param name="Name">The process name (e.g. "chrome.exe").</param>
/// <param name="Path">The full path to the executable, if available.</param>
/// <param name="StartTime">The time the process was started, used to detect PID reuse.</param>
public sealed record ProcessInfo(
    int Pid,
    string Name,
    string? Path,
    DateTime StartTime);

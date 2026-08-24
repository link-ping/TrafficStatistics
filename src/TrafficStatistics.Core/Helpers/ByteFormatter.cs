namespace TrafficStatistics.Core.Helpers;

/// <summary>
/// Provides static methods for formatting byte counts and transfer speeds
/// into human-readable strings.
/// </summary>
public static class ByteFormatter
{
    private static readonly string[] SizeUnits = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    /// Formats a byte count into a human-readable string (e.g. "1.23 GB", "456 KB", "12 B").
    /// </summary>
    /// <param name="bytes">The number of bytes.</param>
    /// <returns>A formatted string representing the byte count.</returns>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            return $"-{FormatBytes(-bytes)}";
        }

        if (bytes == 0)
        {
            return "0 B";
        }

        double value = bytes;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < SizeUnits.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{(long)value} {SizeUnits[unitIndex]}"
            : $"{value:F2} {SizeUnits[unitIndex]}";
    }

    /// <summary>
    /// Formats a speed in bytes per second into a human-readable string
    /// (e.g. "1.23 MB/s", "456 KB/s").
    /// </summary>
    /// <param name="bytesPerSec">The speed in bytes per second.</param>
    /// <returns>A formatted string representing the speed.</returns>
    public static string FormatSpeed(long bytesPerSec)
    {
        return $"{FormatBytes(bytesPerSec)}/s";
    }
}

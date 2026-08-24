namespace TrafficStatistics.Core.Helpers;

/// <summary>
/// Provides static methods for common date/time range calculations
/// and Unix timestamp conversions.
/// </summary>
public static class TimeRangeHelper
{
    /// <summary>
    /// Gets the start and end of the hour containing the specified time.
    /// </summary>
    /// <param name="dt">The reference date/time.</param>
    /// <returns>A tuple of (start, end) where end is exclusive (start of next hour).</returns>
    public static (DateTime start, DateTime end) GetHourRange(DateTime dt)
    {
        var start = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, dt.Kind);
        var end = start.AddHours(1);
        return (start, end);
    }

    /// <summary>
    /// Gets the start and end of the day containing the specified time.
    /// </summary>
    /// <param name="dt">The reference date/time.</param>
    /// <returns>A tuple of (start, end) where end is exclusive (start of next day).</returns>
    public static (DateTime start, DateTime end) GetDayRange(DateTime dt)
    {
        var start = dt.Date;
        var end = start.AddDays(1);
        return (start, end);
    }

    /// <summary>
    /// Gets the start and end of the ISO week (Monday–Sunday) containing the specified time.
    /// </summary>
    /// <param name="dt">The reference date/time.</param>
    /// <returns>A tuple of (start, end) where start is Monday and end is exclusive (next Monday).</returns>
    public static (DateTime start, DateTime end) GetWeekRange(DateTime dt)
    {
        int diff = ((int)dt.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var start = dt.Date.AddDays(-diff);
        var end = start.AddDays(7);
        return (start, end);
    }

    /// <summary>
    /// Gets the start and end of the month containing the specified time.
    /// </summary>
    /// <param name="dt">The reference date/time.</param>
    /// <returns>A tuple of (start, end) where end is exclusive (start of next month).</returns>
    public static (DateTime start, DateTime end) GetMonthRange(DateTime dt)
    {
        var start = new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, dt.Kind);
        var end = start.AddMonths(1);
        return (start, end);
    }

    /// <summary>
    /// Converts a <see cref="DateTime"/> to a Unix timestamp (seconds since 1970-01-01 UTC).
    /// </summary>
    /// <param name="dt">The date/time to convert (treated as UTC).</param>
    /// <returns>Unix timestamp in seconds.</returns>
    public static long ToUnixTimestamp(DateTime dt)
    {
        return new DateTimeOffset(dt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : dt)
            .ToUnixTimeSeconds();
    }

    /// <summary>
    /// Converts a Unix timestamp to a <see cref="DateTime"/> in UTC.
    /// </summary>
    /// <param name="ts">Unix timestamp in seconds.</param>
    /// <returns>The corresponding UTC date/time.</returns>
    public static DateTime FromUnixTimestamp(long ts)
    {
        return DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
    }

    /// <summary>
    /// Aligns a Unix timestamp down to the start of its containing minute.
    /// </summary>
    /// <param name="unixTimestamp">Unix timestamp in seconds.</param>
    /// <returns>The aligned timestamp (multiple of 60).</returns>
    public static long AlignToMinute(long unixTimestamp)
    {
        return unixTimestamp - (unixTimestamp % 60);
    }
}

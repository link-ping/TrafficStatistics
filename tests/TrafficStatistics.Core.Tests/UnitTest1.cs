using TrafficStatistics.Core.Helpers;
using Xunit;

namespace TrafficStatistics.Core.Tests;

public class CoreHelperTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1536, "1.50 KB")]
    [InlineData(1048576, "1.00 MB")]
    [InlineData(1073741824, "1.00 GB")]
    public void FormatBytes_ShouldFormatCorrectly(long bytes, string expected)
    {
        var result = ByteFormatter.FormatBytes(bytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(1024, "1.00 KB/s")]
    [InlineData(1048576, "1.00 MB/s")]
    public void FormatSpeed_ShouldFormatCorrectly(long bytesPerSec, string expected)
    {
        var result = ByteFormatter.FormatSpeed(bytesPerSec);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AlignToMinute_ShouldRoundToNearestMinuteStart()
    {
        // 2026-06-16 10:15:30 UTC
        long original = 1781604930; 
        // 2026-06-16 10:15:00 UTC
        long expected = 1781604900; 

        long result = TimeRangeHelper.AlignToMinute(original);
        Assert.Equal(expected, result);
    }
}

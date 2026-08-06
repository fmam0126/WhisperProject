using WhisperProject.Class;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for <see cref="WhisperClient.FormatSrtTime"/>.
/// NOTE: The <c>hh</c> TimeSpan custom format specifier renders hours
/// <em>not counted as part of days</em> — the day component is always
/// stripped, so values ≥ 24h roll over.
/// </summary>
public class WhisperClientFormatSrtTimeTests
{
    [Fact]
    public void FormatSrtTimeZeroReturnsMidnight()
    {
        var result = WhisperClient.FormatSrtTime(TimeSpan.Zero);
        Assert.Equal("00:00:00,000", result);
    }

    [Theory]
    [InlineData(0, "00:00:00,000")]
    [InlineData(1, "00:00:00,001")]
    [InlineData(10, "00:00:00,010")]
    [InlineData(100, "00:00:00,100")]
    [InlineData(999, "00:00:00,999")]
    public void FormatSrtTimeMillisecondsThreeDigitPadded(int ms, string expected)
    {
        var result = WhisperClient.FormatSrtTime(TimeSpan.FromMilliseconds(ms));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatSrtTimeMinutesAndSeconds()
    {
        var result = WhisperClient.FormatSrtTime(new TimeSpan(0, 1, 2));
        Assert.Equal("00:01:02,000", result);
    }

    [Fact]
    public void FormatSrtTimeMillisecondsComponent()
    {
        // 1s 2s 3ms → "00:01:02,003"
        var result = WhisperClient.FormatSrtTime(new TimeSpan(0, 0, 1, 2, 3));
        Assert.Equal("00:01:02,003", result);
    }

    [Fact]
    public void FormatSrtTimeHoursIncluded()
    {
        var result = WhisperClient.FormatSrtTime(new TimeSpan(1, 2, 3));
        Assert.Equal("01:02:03,000", result);
    }

    [Fact]
    public void FormatSrtTimeOverTwentyFourHoursShouldBeIncluded()
    {
        // The 'hh' specifier strips days. 25h → 1h.
        var result = WhisperClient.FormatSrtTime(new TimeSpan(25, 0, 0));
        Assert.Equal("25:00:00,000", result);
    }

    [Fact]
    public void FormatSrtTimeNegativeReturnsZeroedTime()
    {
        var result = WhisperClient.FormatSrtTime(TimeSpan.FromSeconds(-1));
        Assert.Equal("00:00:00,000", result);
    }

    [Fact]
    public void FormatSrtTimeLargeValueReturnsTotalHours()
    {
        // 1 day + 2h 30m → 02:30:00,000
        var result = WhisperClient.FormatSrtTime(new TimeSpan(1, 2, 30, 0));
        Assert.Equal("26:30:00,000", result);
    }
}

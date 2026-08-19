using WhisperProject.Core;
using WhisperProject.Models;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for <see cref="DownloadProgressFormat"/> — the shared progress
/// formatting used by the console reporter and the GUI status label.
/// </summary>
public class DownloadProgressFormatTests
{
    [Fact]
    public void FormatKnownFractionUsesPercent()
    {
        Assert.Equal("50.00%", DownloadProgressFormat.FormatFraction(0.5));
    }

    [Fact]
    public void FormatNegativeFractionShowsUnknown()
    {
        Assert.Equal("unknown", DownloadProgressFormat.FormatFraction(-1));
    }

    [Fact]
    public void FormatZeroFractionShowsZeroPercent()
    {
        Assert.Equal("0.00%", DownloadProgressFormat.FormatFraction(0));
    }

    [Fact]
    public void FormatIncludesSpeedInMegabytesPerSecond()
    {
        var result = DownloadProgressFormat.Format(new DownloadProgress(0.5, 12.34));

        Assert.Equal("50.00% | 12.34 MB/s", result);
    }

    [Fact]
    public void FormatUnknownFractionWithSpeed()
    {
        var result = DownloadProgressFormat.Format(new DownloadProgress(-1, 3.5));

        Assert.Equal("unknown | 3.50 MB/s", result);
    }
}

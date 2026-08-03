using WhisperProject.Class;
using VadTimeMapping = WhisperProject.Class.WhisperClient.VadTimeMapping;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for <see cref="WhisperClient.MapToOriginalTime"/> — the
/// binary-search interpolation that maps processed-time ticks back to
/// original-time ticks after Voice Activity Detection filtering.
/// </summary>
public class WhisperClientMapToOriginalTimeTests
{
    /// <summary>Helper: creates a mapping table from parallel arrays.</summary>
    private static List<VadTimeMapping> MakeTable(
        long[] processed, long[] original)
    {
        var table = new List<VadTimeMapping>(processed.Length);
        for (int i = 0; i < processed.Length; i++)
            table.Add(new VadTimeMapping(processed[i], original[i]));
        return table;
    }

    [Fact]
    public void EmptyTable_ReturnsInputTicksUnchanged()
    {
        var result = WhisperClient.MapToOriginalTime(1000, new List<VadTimeMapping>());

        Assert.Equal(TimeSpan.FromTicks(1000), result);
    }

    [Fact]
    public void ExactMatch_ReturnsOriginalTime()
    {
        var table = MakeTable(
            [0, 1000, 2000],
            [0, 2000, 4000]);

        var result = WhisperClient.MapToOriginalTime(1000, table);

        Assert.Equal(TimeSpan.FromTicks(2000), result);
    }

    [Fact]
    public void BetweenPoints_Midpoint_LinearInterpolation()
    {
        // processed  0     1000    2000
        // original   0     2000    4000
        // t=500 → ratio 0.5 → orig=1000
        var table = MakeTable(
            [0, 1000, 2000],
            [0, 2000, 4000]);

        var result = WhisperClient.MapToOriginalTime(500, table);

        Assert.Equal(TimeSpan.FromTicks(1000), result);
    }

    [Fact]
    public void BetweenPoints_QuarterPoint_LinearInterpolation()
    {
        // processed  0     1000   2000
        // original   0     2000   4000
        // t=250 → ratio 0.25 → orig=500
        var table = MakeTable(
            [0, 1000, 2000],
            [0, 2000, 4000]);

        var result = WhisperClient.MapToOriginalTime(250, table);

        Assert.Equal(TimeSpan.FromTicks(500), result);
    }

    [Fact]
    public void AfterLastPoint_DeltaOffset()
    {
        // Last point: processed=2000, original=4000
        // t=2500 → delta=500 → 4000+500=4500
        var table = MakeTable(
            [0, 1000, 2000],
            [0, 2000, 4000]);

        var result = WhisperClient.MapToOriginalTime(2500, table);

        Assert.Equal(TimeSpan.FromTicks(4500), result);
    }

    [Fact]
    public void BeforeFirstPoint_WhenFirstProcessedIsZero_ReturnsZero()
    {
        var table = MakeTable([0, 1000], [0, 2000]);

        var result = WhisperClient.MapToOriginalTime(-500, table);

        Assert.Equal(TimeSpan.FromTicks(0), result);
    }

    [Fact]
    public void BeforeFirstPoint_WhenFirstProcessedIsNotZero_ScalesByRatio()
    {
        // First point: processed=1000, original=2000
        // t=500 → ratio 0.5 → orig=1000
        var table = MakeTable([1000], [2000]);

        var result = WhisperClient.MapToOriginalTime(500, table);

        Assert.Equal(TimeSpan.FromTicks(1000), result);
    }

    [Fact]
    public void AdjacentEqualProcessedTimes_UsesPreviousOriginal()
    {
        // Processed: 0, 1000, 1000 (duplicate) — dedup logic removes the
        // duplicate before use, but the test sends the raw (duplicate) table
        // to exercise the `next.ProcessedTimeTicks == prev.ProcessedTimeTicks` guard
        // in the interpolation branch.
        // processed  0     1000    1000  2000
        // original   0     1000    2000  3000
        // t=999 → between idx 0 (0→0) and idx 1 (1000→1000)
        // ratio = 999/1000 → orig = 999
        var table = MakeTable(
            [0, 1000, 1000, 2000],
            [0, 1000, 2000, 3000]);

        // t=999 falls between processed[0]=0 and processed[1]=1000
        var result = WhisperClient.MapToOriginalTime(999, table);

        Assert.Equal(TimeSpan.FromTicks(999), result);
    }

    [Fact]
    public void SinglePointTable_BeforeFirst_ScalesByRatio()
    {
        var table = MakeTable([500], [1000]);

        var result = WhisperClient.MapToOriginalTime(250, table);

        // ratio = 250/500 = 0.5 → orig = 500
        Assert.Equal(TimeSpan.FromTicks(500), result);
    }

    [Fact]
    public void SinglePointTable_AfterLast_DeltaOffset()
    {
        var table = MakeTable([500], [1000]);

        var result = WhisperClient.MapToOriginalTime(1000, table);

        // delta = 1000-500 = 500 → orig = 1000+500 = 1500
        Assert.Equal(TimeSpan.FromTicks(1500), result);
    }
}

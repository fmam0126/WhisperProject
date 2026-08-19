using WhisperProject.Core;
using WhisperProject.Models;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for <see cref="ModelDownloader.CopyWithProgressAsync"/> — the
/// streaming copy core exercised with in-memory streams (no network).
/// </summary>
public class ModelDownloaderTests
{
    private sealed class CollectingProgress : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Reports { get; } = new();

        public void Report(DownloadProgress value) => Reports.Add(value);
    }

    private static byte[] CreateSource(int size)
    {
        var data = new byte[size];
        new Random(1234).NextBytes(data);
        return data;
    }

    [Fact]
    public async Task CopyWithProgressAsyncCopiesAllBytes()
    {
        var source = CreateSource(1024);
        var progress = new CollectingProgress();
        using var destination = new MemoryStream();

        await ModelDownloader.CopyWithProgressAsync(
            new MemoryStream(source), source.Length, destination, progress);

        Assert.Equal(source, destination.ToArray());
    }

    [Fact]
    public async Task CopyWithProgressAsyncReportsZeroAtStart()
    {
        var source = CreateSource(1024);
        var progress = new CollectingProgress();
        using var destination = new MemoryStream();

        await ModelDownloader.CopyWithProgressAsync(
            new MemoryStream(source), source.Length, destination, progress);

        Assert.NotEmpty(progress.Reports);
        Assert.Equal(0, progress.Reports[0].Fraction);
        Assert.Equal(0, progress.Reports[0].MegabytesPerSecond);
    }

    [Fact]
    public async Task CopyWithProgressAsyncReportsIncreasingFractions()
    {
        var source = CreateSource(8192);
        var progress = new CollectingProgress();
        using var destination = new MemoryStream();

        await ModelDownloader.CopyWithProgressAsync(
            new MemoryStream(source), source.Length, destination, progress);

        var fractions = progress.Reports.Select(r => r.Fraction).ToList();
        for (int i = 1; i < fractions.Count; i++)
        {
            Assert.True(fractions[i] >= fractions[i - 1],
                $"fraction decreased: {fractions[i - 1]} -> {fractions[i]}");
        }
    }

    [Fact]
    public async Task CopyWithProgressAsyncFinalReportFractionOne()
    {
        var source = CreateSource(1024);
        var progress = new CollectingProgress();
        using var destination = new MemoryStream();

        await ModelDownloader.CopyWithProgressAsync(
            new MemoryStream(source), source.Length, destination, progress);

        Assert.Equal(1, progress.Reports[^1].Fraction);
    }

    [Fact]
    public async Task CopyWithProgressAsyncUnknownTotalReportsNegativeFraction()
    {
        var source = CreateSource(1024);
        var progress = new CollectingProgress();
        using var destination = new MemoryStream();

        await ModelDownloader.CopyWithProgressAsync(
            new MemoryStream(source), totalBytes: null, destination, progress);

        Assert.NotEmpty(progress.Reports);
        Assert.Contains(progress.Reports, r => r.Fraction < 0);
        Assert.Equal(1, progress.Reports[^1].Fraction);
    }

    [Fact]
    public async Task CopyWithProgressAsyncReportsPositiveSpeed()
    {
        var source = CreateSource(4096);
        var progress = new CollectingProgress();
        using var destination = new MemoryStream();

        await ModelDownloader.CopyWithProgressAsync(
            new MemoryStream(source), source.Length, destination, progress);

        Assert.NotEmpty(progress.Reports);
        Assert.Contains(progress.Reports, r => r.MegabytesPerSecond > 0);
    }

    [Fact]
    public async Task CopyWithProgressAsyncThrowsOnCancellation()
    {
        var source = CreateSource(1024);
        var progress = new CollectingProgress();
        using var destination = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ModelDownloader.CopyWithProgressAsync(
                new MemoryStream(source), source.Length, destination, progress, cts.Token));
    }

    [Fact]
    public async Task CopyWithProgressAsyncEmptySourceReportsStartAndFinal()
    {
        var progress = new CollectingProgress();
        using var destination = new MemoryStream();

        await ModelDownloader.CopyWithProgressAsync(
            new MemoryStream(Array.Empty<byte>()), 0, destination, progress);

        // start report (0, 0) followed by the final report (1, 0)
        Assert.Equal(2, progress.Reports.Count);
        Assert.Equal(0, progress.Reports[0].Fraction);
        Assert.Equal(1, progress.Reports[^1].Fraction);
    }
}

using WhisperProject.Core;
using WhisperProject.Models;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for <see cref="ProgressReportingStream"/> using in-memory streams and
/// a zero report interval for deterministic per-read reports.
/// </summary>
public class ProgressReportingStreamTests
{
    private sealed class CollectingProgress : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Reports { get; } = new();

        public void Report(DownloadProgress value) => Reports.Add(value);
    }

    private static (ProgressReportingStream Stream, CollectingProgress Progress) Create(
        byte[] source, TimeSpan? minReportInterval = null)
    {
        var progress = new CollectingProgress();
        var stream = new ProgressReportingStream(
            new MemoryStream(source),
            progress,
            minReportInterval ?? TimeSpan.Zero);
        return (stream, progress);
    }

    [Fact]
    public void ReadsThroughAllBytesFromInnerStream()
    {
        var source = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var (stream, _) = Create(source);

        var buffer = new byte[8];
        var totalRead = 0;
        int read;
        while ((read = stream.Read(buffer, totalRead, buffer.Length - totalRead)) > 0)
        {
            totalRead += read;
        }

        Assert.Equal(source.Length, totalRead);
        Assert.Equal(source, buffer);
    }

    [Fact]
    public void ReportsFractionAtEachRead()
    {
        var source = new byte[8];
        var (stream, progress) = Create(source);

        var buffer = new byte[4];
        var firstRead = stream.Read(buffer, 0, 4); // 4/8 → 0.5
        var secondRead = stream.Read(buffer, 0, 4); // 8/8 → 1.0

        Assert.Equal(4, firstRead);
        Assert.Equal(4, secondRead);
        Assert.Equal(2, progress.Reports.Count);
        Assert.Equal(0.5, progress.Reports[0].Fraction, 3);
        Assert.Equal(1.0, progress.Reports[1].Fraction, 3);
    }

    [Fact]
    public void FractionMonotonicNonDecreasing()
    {
        var source = new byte[64];
        var (stream, progress) = Create(source);

        var buffer = new byte[8];
        while (stream.Read(buffer, 0, buffer.Length) > 0) { }

        var fractions = progress.Reports.Select(r => r.Fraction).ToList();
        for (int i = 1; i < fractions.Count; i++)
        {
            Assert.True(fractions[i] >= fractions[i - 1],
                $"fraction decreased: {fractions[i - 1]} -> {fractions[i]}");
        }
    }

    [Fact]
    public void ReportedSpeedNonNegative()
    {
        var source = new byte[64];
        var (stream, progress) = Create(source);

        var buffer = new byte[8];
        while (stream.Read(buffer, 0, buffer.Length) > 0) { }

        Assert.NotEmpty(progress.Reports);
        Assert.All(progress.Reports, r => Assert.True(r.MegabytesPerSecond >= 0));
    }

    [Fact]
    public void EmptyStreamDoesNotDivideByZero()
    {
        var source = Array.Empty<byte>();
        var (stream, progress) = Create(source);

        var read = stream.Read(new byte[4], 0, 4);

        Assert.Equal(0, read);
        Assert.Empty(progress.Reports); // no reports — nothing read, no NaN produced
    }

    [Fact]
    public void WriteThrowsNotSupported()
    {
        var (stream, _) = Create(new byte[4]);

        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[4], 0, 4));
    }

    [Fact]
    public void SetLengthThrowsNotSupported()
    {
        var (stream, _) = Create(new byte[4]);

        Assert.Throws<NotSupportedException>(() => stream.SetLength(10));
    }

    [Fact]
    public async Task ReadAsyncReportsProgress()
    {
        var source = new byte[8];
        var progress = new CollectingProgress();
        var stream = new ProgressReportingStream(new MemoryStream(source), progress, TimeSpan.Zero);

        var buffer = new byte[4];
        var firstRead = await stream.ReadAsync(buffer, 0, 4);
        var secondRead = await stream.ReadAsync(buffer, 0, 4);

        Assert.Equal(4, firstRead);
        Assert.Equal(4, secondRead);
        Assert.Equal(2, progress.Reports.Count);
        Assert.Equal(1.0, progress.Reports[1].Fraction, 3);
    }
}

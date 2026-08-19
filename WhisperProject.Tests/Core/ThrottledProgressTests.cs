using WhisperProject.Core;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for <see cref="ThrottledProgress{T}"/> using a collecting fake as the
/// inner reporter. Uses extreme intervals so tests are deterministic without
/// real waiting.
/// </summary>
public class ThrottledProgressTests
{
    private sealed class CollectingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = new();

        public void Report(T value) => Values.Add(value);
    }

    private static ThrottledProgress<int> Create(CollectingProgress<int> inner, TimeSpan interval) =>
        new(inner, interval, isFinal: v => v >= 100);

    [Fact]
    public void FirstReportForwardedImmediately()
    {
        var inner = new CollectingProgress<int>();
        var throttled = Create(inner, TimeSpan.FromHours(1));

        throttled.Report(10);

        Assert.Single(inner.Values);
        Assert.Equal(10, inner.Values[0]);
    }

    [Fact]
    public void ReportsWithinIntervalSuppressed()
    {
        var inner = new CollectingProgress<int>();
        var throttled = Create(inner, TimeSpan.FromHours(1));

        throttled.Report(10);
        throttled.Report(20);
        throttled.Report(30);

        Assert.Single(inner.Values);
        Assert.Equal(10, inner.Values[0]);
    }

    [Fact]
    public void ReportAfterIntervalElapsedForwarded()
    {
        var inner = new CollectingProgress<int>();
        // Zero interval → every report is forwarded
        var throttled = Create(inner, TimeSpan.Zero);

        throttled.Report(10);
        throttled.Report(20);
        throttled.Report(30);

        Assert.Equal([10, 20, 30], inner.Values);
    }

    [Fact]
    public void FinalReportAlwaysForwardedEvenWithinInterval()
    {
        var inner = new CollectingProgress<int>();
        var throttled = Create(inner, TimeSpan.FromHours(1));

        throttled.Report(10);   // forwarded (first)
        throttled.Report(20);   // suppressed
        throttled.Report(100);  // final — always forwarded

        Assert.Equal([10, 100], inner.Values);
    }

    [Fact]
    public void ForwardedValuesMatchReportsInOrder()
    {
        var inner = new CollectingProgress<int>();
        var throttled = Create(inner, TimeSpan.Zero);

        throttled.Report(5);
        throttled.Report(50);
        throttled.Report(100);

        Assert.Equal([5, 50, 100], inner.Values);
    }
}

using WhisperProject.Core;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for <see cref="RelayProgress{T}"/> — the synchronous
/// <see cref="IProgress{T}"/> used instead of <see cref="Progress{T}"/>.
/// </summary>
public class RelayProgressTests
{
    [Fact]
    public void RelayProgressInvokesHandlerSynchronouslyWithValue()
    {
        var received = new List<int>();
        var progress = new RelayProgress<int>(received.Add);

        progress.Report(42);

        Assert.Single(received);
        Assert.Equal(42, received[0]);
    }

    [Fact]
    public void RelayProgressInvokesHandlerForEveryReport()
    {
        var received = new List<int>();
        var progress = new RelayProgress<int>(received.Add);

        progress.Report(1);
        progress.Report(2);
        progress.Report(3);

        Assert.Equal([1, 2, 3], received);
    }
}

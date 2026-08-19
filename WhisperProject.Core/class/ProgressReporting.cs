using System.Diagnostics;

namespace WhisperProject.Core;

/// <summary>
/// An <see cref="IProgress{T}"/> that invokes its handler synchronously on the reporting thread.
/// Used instead of <see cref="Progress{T}"/> because Progress&lt;T&gt; posts callbacks to the
/// thread pool when no SynchronizationContext is captured, losing ordering guarantees.
/// </summary>
public sealed class RelayProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public RelayProgress(Action<T> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void Report(T value) => _handler(value);
}

/// <summary>
/// Forwards at most one report per <paramref name="minInterval"/>.
/// Reports matching <paramref name="isFinal"/> are always forwarded immediately
/// so the terminal state is never dropped by throttling.
/// </summary>
public sealed class ThrottledProgress<T> : IProgress<T>
{
    private readonly IProgress<T> _inner;
    private readonly TimeSpan _minInterval;
    private readonly Func<T, bool> _isFinal;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan _lastForwarded;
    private bool _hasForwarded;

    public ThrottledProgress(IProgress<T> inner, TimeSpan minInterval, Func<T, bool> isFinal)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _minInterval = minInterval;
        _isFinal = isFinal ?? throw new ArgumentNullException(nameof(isFinal));
    }

    public void Report(T value)
    {
        if (_isFinal(value) || !_hasForwarded || _stopwatch.Elapsed - _lastForwarded >= _minInterval)
        {
            _hasForwarded = true;
            _lastForwarded = _stopwatch.Elapsed;
            _inner.Report(value);
        }
    }
}

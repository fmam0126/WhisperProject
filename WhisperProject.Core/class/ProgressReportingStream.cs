using System.Diagnostics;
using WhisperProject.Models;

namespace WhisperProject.Core;

/// <summary>
/// Wraps a readable stream, reporting bytes-read progress (throttled).
/// Byte-count based (not Position) so it is correct even if the inner stream is seeked.
/// </summary>
internal sealed class ProgressReportingStream : Stream
{
    private readonly Stream _inner;
    private readonly IProgress<DownloadProgress>? _progress;
    private readonly long _totalLength;
    private readonly TimeSpan _minReportInterval;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _bytesRead;
    private TimeSpan _lastReport;
    private double _lastSpeed;

    internal ProgressReportingStream(Stream inner, IProgress<DownloadProgress>? progress, TimeSpan? minReportInterval = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _progress = progress;
        _totalLength = inner.Length;
        _minReportInterval = minReportInterval ?? TimeSpan.FromMilliseconds(100);
    }

    internal double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds;

    internal double CurrentSpeed => _lastSpeed;

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => _inner.CanSeek;

    public override bool CanWrite => false;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush() => _inner.Flush();

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        ReportIfNeeded(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        ReportIfNeeded(read);
        return read;
    }

    private void ReportIfNeeded(int bytesRead)
    {
        if (bytesRead <= 0)
            return;

        _bytesRead += bytesRead;
        var elapsed = _stopwatch.Elapsed;
        var elapsedSeconds = elapsed.TotalSeconds;
        _lastSpeed = elapsedSeconds > 0
            ? _bytesRead / 1024d / 1024d / elapsedSeconds
            : 0;

        if (elapsed - _lastReport < _minReportInterval)
            return;

        _lastReport = elapsed;
        var fraction = _totalLength > 0
            ? (double)_bytesRead / _totalLength
            : -1;
        _progress?.Report(new DownloadProgress(fraction, _lastSpeed));
    }
}

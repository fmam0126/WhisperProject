using System.Diagnostics;
using WhisperProject.Models;

namespace WhisperProject.Core;

/// <summary>
/// Downloads model files over HTTP with progress and speed reporting.
/// </summary>
public static class ModelDownloader
{
    private const int BufferSize = 81920;
    private const int DownloadTimeoutMinutes = 8;

    /// <summary>
    /// Downloads a file to <paramref name="destinationPath"/>, reporting progress.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="destinationPath">The local path to write the file to.</param>
    /// <param name="progress">Optional progress reporter. Receives fraction (0-1, or -1 when the total size is unknown) and speed in MB/s.</param>
    /// <param name="cancellationToken">Cancellation token for the download.</param>
    public static async Task DownloadAsync(
        string url,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL must not be empty.", nameof(url));
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path must not be empty.", nameof(destinationPath));

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(DownloadTimeoutMinutes) };
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            useAsync: true);
        await CopyWithProgressAsync(source, totalBytes, destination, progress, cancellationToken);
    }

    /// <summary>
    /// Streams <paramref name="source"/> to <paramref name="destination"/> with progress reports.
    /// Internal so the reporting loop is unit-testable with in-memory streams (no network).
    /// </summary>
    internal static async Task CopyWithProgressAsync(
        Stream source,
        long? totalBytes,
        Stream destination,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[BufferSize];
        long downloadedBytes = 0;
        var stopwatch = Stopwatch.StartNew();

        progress?.Report(new DownloadProgress(0, 0));

        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;

            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            var megabytesPerSecond = elapsedSeconds > 0
                ? downloadedBytes / 1024d / 1024d / elapsedSeconds
                : 0;
            var fraction = totalBytes.HasValue && totalBytes.Value > 0
                ? (double)downloadedBytes / totalBytes.Value
                : -1;

            progress?.Report(new DownloadProgress(fraction, megabytesPerSecond));
        }

        var finalElapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        var finalSpeed = finalElapsedSeconds > 0
            ? downloadedBytes / 1024d / 1024d / finalElapsedSeconds
            : 0;
        progress?.Report(new DownloadProgress(1, finalSpeed));
    }
}

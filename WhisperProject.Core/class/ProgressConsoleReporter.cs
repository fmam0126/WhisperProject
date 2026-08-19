using System.Globalization;
using WhisperProject.Models;

namespace WhisperProject.Core;

/// <summary>
/// Formats a <see cref="DownloadProgress"/> for display; shared by the console reporter
/// and the GUI status label. Culture-invariant so output is identical everywhere.
/// </summary>
public static class DownloadProgressFormat
{
    public static string FormatFraction(double fraction) =>
        fraction >= 0 ? (fraction * 100).ToString("F2", CultureInfo.InvariantCulture) + "%" : "unknown";

    public static string Format(DownloadProgress progress) =>
        $"{FormatFraction(progress.Fraction)} | {progress.MegabytesPerSecond.ToString("F2", CultureInfo.InvariantCulture)} MB/s";
}

/// <summary>
/// Creates console progress reporters that rewrite a single line in place.
/// </summary>
public static class ProgressConsoleReporter
{
    /// <summary>
    /// Rewrites one console line ("{prefix}: 45.00% | 12.34 MB/s") and emits a newline
    /// when the operation completes. Synchronous — safe for use on any thread.
    /// </summary>
    public static IProgress<DownloadProgress> Create(string prefix = "Download progress") =>
        new RelayProgress<DownloadProgress>(status =>
        {
            var message = $"{prefix}: {DownloadProgressFormat.Format(status)}";
            Console.Write($"\r{message.PadRight(40)}");
            if (status.Fraction >= 1)
                Console.WriteLine();
        });
}

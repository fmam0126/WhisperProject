namespace WhisperProject.Models;

/// <summary>
/// Progress of a model download or archive extraction.
/// Fraction is -1 when the total size is unknown, and 1 when complete.
/// </summary>
public sealed record DownloadProgress(double Fraction, double MegabytesPerSecond);

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WhisperProject.Class;
using WhisperProject.Core;
using WhisperProject.Models;

namespace WhisperProject.Avalonia.Services;

/// <summary>
/// Orchestrates the full transcription pipeline — media conversion, optional
/// voice enhancement, Whisper transcription, and LLM-based subtitle translation —
/// reporting progress back to the UI via events. Handles both single-file and
/// folder (batch) modes.
/// </summary>
public class TranscriptionService
{
    private readonly Settings _settings;
    private readonly string _inputPath;
    private readonly bool _isFolder;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Raised when a progress message is available. Subscribers should marshal
    /// to the UI thread as needed — this event may fire from a background thread.
    /// </summary>
    public event Action<string>? ProgressChanged;

    /// <summary>
    /// Raised when all processing has completed. The string argument is a
    /// human-readable summary of successes and failures.
    /// </summary>
    public event Action<string>? ProcessingCompleted;

    /// <summary>
    /// Raised when an individual file fails to process. Arguments are the file
    /// name and the error message.
    /// </summary>
    public event Action<string, string>? FileFailed;

    /// <summary>
    /// Creates a new transcription service for the given input.
    /// </summary>
    /// <param name="settings">All transcription/translation configuration.</param>
    /// <param name="inputPath">Path to a single media file or a folder.</param>
    /// <param name="isFolder">
    /// True if <paramref name="inputPath"/> is a folder (batch mode);
    /// false if it is a single file.
    /// </param>
    public TranscriptionService(Settings settings, string inputPath, bool isFolder)
    {
        _settings = settings;
        _inputPath = inputPath;
        _isFolder = isFolder;
    }

    /// <summary>
    /// Runs the transcription pipeline asynchronously. Progress is reported
    /// via <see cref="ProgressChanged"/> throughout.
    /// </summary>
    public async Task ProcessAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        try
        {
            List<string> sourceFiles;

            if (_isFolder)
            {
                ReportProgress($"Scanning folder: {_inputPath}");
                sourceFiles = FolderParser.FindSourceFiles(_inputPath);
                ReportProgress($"Found {sourceFiles.Count} media file(s) in folder.");
            }
            else
            {
                var extension = Path.GetExtension(_inputPath).ToLowerInvariant();
                if (extension is not (".mp4" or ".mkv" or ".mp3"))
                {
                    ReportProgress(
                        $"Warning: '{extension}' may not be a supported media type. " +
                        "Attempting anyway.");
                }
                sourceFiles = new List<string> { _inputPath };
            }

            if (sourceFiles.Count == 0)
            {
                ReportProgress("No media files found to process.");
                ProcessingCompleted?.Invoke("No files to process.");
                return;
            }

            var subtitleTranslator = new SubtitleTranslator
            {
                Url = _settings.Url,
                Port = _settings.Port,
                TargetLanguage = _settings.TargetLanguage,
                ApiKey = _settings.ApiKey,
                GptPath = _settings.GptPath,
                Model = _settings.GptModel,
                Concurrency = _settings.Concurrency,
                ContextSize = _settings.ContextSize,
                SystemPrompt = _settings.SystemPrompt
            };

            int successCount = 0;
            int failCount = 0;

            for (int fileIndex = 0; fileIndex < sourceFiles.Count; fileIndex++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    ReportProgress("Processing cancelled by user.");
                    break;
                }

                var sourceFile = sourceFiles[fileIndex];
                var fileName = Path.GetFileName(sourceFile);
                ReportProgress(
                    $"[{fileIndex + 1}/{sourceFiles.Count}] Processing: {fileName}");

                try
                {
                    await ProcessSingleFile(sourceFile, subtitleTranslator, cancellationToken);
                    successCount++;
                    ReportProgress($"✓ Completed: {fileName}");
                }
                catch (OperationCanceledException)
                {
                    ReportProgress("Processing cancelled.");
                    break;
                }
                catch (Exception exception)
                {
                    failCount++;
                    ReportProgress($"✗ Failed: {fileName} — {exception.Message}");
                    FileFailed?.Invoke(fileName, exception.Message);
                }
            }

            var summary = $"Processing finished. {successCount} succeeded, {failCount} failed.";
            ReportProgress(summary);
            ProcessingCompleted?.Invoke(summary);
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// Requests cancellation of the running transcription pipeline.
    /// Processing stops at the next safe checkpoint.
    /// </summary>
    public void Cancel()
    {
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// Processes a single media file through the full pipeline:
    /// convert → (optional enhance) → transcribe → translate → cleanup.
    /// </summary>
    private async Task ProcessSingleFile(
        string sourceFile,
        SubtitleTranslator subtitleTranslator,
        CancellationToken cancellationToken)
    {
        var fileConvert = new FileConvert(_settings.InputPath);

        // Step 1: Convert media file to 16kHz mono WAV
        ReportProgress($"  Converting to WAV: {Path.GetFileName(sourceFile)}");
        var outputPath = await fileConvert.ConvertToWav(sourceFile);
        cancellationToken.ThrowIfCancellationRequested();

        // Step 2: Optional DpdfNet voice enhancement
        if (_settings.ApplyDpdfNet)
        {
            ReportProgress("  Applying DpdfNet voice enhancement...");
            var filter = new VoiceEmphasisFilter();
            var filteredOutputPath = Path.Combine(
                Path.GetDirectoryName(outputPath) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(outputPath)}.filtered.wav");

            await filter.ApplyDpdfNetVoiceEnhancement(
                outputPath, filteredOutputPath,
                _settings.DpdfNetModelPath, _settings.DpdfNetDownloadUrl);

            File.Copy(filteredOutputPath, outputPath, overwrite: true);
            File.Delete(filteredOutputPath);
            cancellationToken.ThrowIfCancellationRequested();
        }

        // Step 3: Transcribe WAV to SRT (Qwen3 ASR runs its own internal VAD)
        ReportProgress("  Transcribing audio to text...");
        string? identifiedLanguage;
        string modelDir = AppContext.BaseDirectory;
        if (_settings.UseQwen3Asr)
        {
            identifiedLanguage = await Qwen3Asr.RunQwen3Asr(outputPath, modelDir);
        }
        else if (_settings.UseVoiceActivityDetection)
        {
            identifiedLanguage = await WhisperClient.TranscribeVadAsync(
                outputPath,
                modelPath: _settings.WhisperModelPath,
                language: _settings.WhisperLanguage);
        }
        else
        {
            identifiedLanguage = await WhisperClient.TranscribeAsync(
                outputPath,
                language: _settings.WhisperLanguage,
                modelPath: _settings.WhisperModelPath);
        }
        subtitleTranslator.SourceLanguage = identifiedLanguage ?? string.Empty;
        cancellationToken.ThrowIfCancellationRequested();

        // Step 4: Translate the generated SRT via the LLM
        ReportProgress("  Translating subtitles...");
        var srtFileName = Path.Combine(
            Path.GetDirectoryName(outputPath) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(outputPath)}.srt");

        if (_settings.UseContextTranslation)
        {
            await subtitleTranslator.TranslateSrtWithContextAsync(
                srtFileName,
                Path.GetDirectoryName(sourceFile) ?? outputPath);
        }
        else
        {
            await subtitleTranslator.TranslateSrtAsync(
                srtFileName,
                Path.GetDirectoryName(sourceFile) ?? outputPath);
        }
        cancellationToken.ThrowIfCancellationRequested();

        // Step 5: Clean up temporary files (intermediate SRT and WAV)
        try
        {
            if (File.Exists(srtFileName)) File.Delete(srtFileName);
            if (File.Exists(outputPath)) File.Delete(outputPath);

            var tempDirectory = Path.GetDirectoryName(outputPath);
            if (tempDirectory is not null
                && Directory.Exists(tempDirectory)
                && !Directory.EnumerateFileSystemEntries(tempDirectory).Any())
            {
                Directory.Delete(tempDirectory);
            }
        }
        catch (Exception exception)
        {
            ReportProgress($"  Warning: Cleanup issue — {exception.Message}");
        }
    }

    /// <summary>
    /// Fires the <see cref="ProgressChanged"/> event with the given message.
    /// </summary>
    private void ReportProgress(string message)
    {
        ProgressChanged?.Invoke(message);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WhisperProject.Class;
using WhisperProject.Models;

namespace WhisperProject.Avalonia.Services;

/// <summary>
/// Orchestrates the transcription pipeline and reports progress back to the UI.
/// Handles both single-file and folder (batch) modes.
/// </summary>
public class TranscriptionService
{
    private readonly Settings _settings;
    private readonly string _inputPath;
    private readonly bool _isFolder;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Raised when a progress message is available (thread-safe — subscribers should
    /// marshal to the UI thread as needed).
    /// </summary>
    public event Action<string>? ProgressChanged;

    /// <summary>
    /// Raised when processing completes (with a summary message).
    /// </summary>
    public event Action<string>? ProcessingCompleted;

    /// <summary>
    /// Raised when an individual file fails (file name + error).
    /// </summary>
    public event Action<string, string>? FileFailed;

    public TranscriptionService(Settings settings, string inputPath, bool isFolder)
    {
        _settings = settings;
        _inputPath = inputPath;
        _isFolder = isFolder;
    }

    /// <summary>
    /// Starts the transcription pipeline asynchronously.
    /// </summary>
    public async Task ProcessAsync()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

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
                var ext = Path.GetExtension(_inputPath).ToLowerInvariant();
                if (ext is not (".mp4" or ".mkv" or ".mp3"))
                {
                    ReportProgress($"Warning: '{ext}' may not be a supported media type. Attempting anyway.");
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

            for (int i = 0; i < sourceFiles.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    ReportProgress("Processing cancelled by user.");
                    break;
                }

                var item = sourceFiles[i];
                var fileName = Path.GetFileName(item);
                ReportProgress($"[{i + 1}/{sourceFiles.Count}] Processing: {fileName}");

                try
                {
                    await ProcessSingleFile(item, subtitleTranslator, token);
                    successCount++;
                    ReportProgress($"✓ Completed: {fileName}");
                }
                catch (OperationCanceledException)
                {
                    ReportProgress("Processing cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    failCount++;
                    ReportProgress($"✗ Failed: {fileName} — {ex.Message}");
                    FileFailed?.Invoke(fileName, ex.Message);
                }
            }

            var summary = $"Processing finished. {successCount} succeeded, {failCount} failed.";
            ReportProgress(summary);
            ProcessingCompleted?.Invoke(summary);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Cancels the running transcription process.
    /// </summary>
    public void Cancel()
    {
        _cts?.Cancel();
    }

    private async Task ProcessSingleFile(string sourceFile, SubtitleTranslator subtitleTranslator, CancellationToken token)
    {
        var fileConvert = new FileConvert(_settings.InputPath);

        // Step 1: Convert to WAV
        ReportProgress($"  Converting to WAV: {Path.GetFileName(sourceFile)}");
        var outputPath = await fileConvert.ConvertToWav(sourceFile);
        token.ThrowIfCancellationRequested();

        // Step 2: Optional voice enhancement
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
            token.ThrowIfCancellationRequested();
        }

        // Step 3: Transcribe
        ReportProgress("  Transcribing audio to text...");
        if (_settings.UseVoiceActivityDetection)
        {
            await WhisperClient.TranscribeVadAsync(
                outputPath,
                modelPath: _settings.WhisperModelpath,
                language: _settings.WhisperLanguage);
        }
        else
        {
            await WhisperClient.TranscribeAsync(
                outputPath,
                language: _settings.WhisperLanguage,
                modelPath: _settings.WhisperModelpath);
        }
        token.ThrowIfCancellationRequested();

        // Step 4: Translate subtitles
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
        token.ThrowIfCancellationRequested();

        // Step 5: Cleanup temp files
        try
        {
            if (File.Exists(srtFileName)) File.Delete(srtFileName);
            if (File.Exists(outputPath)) File.Delete(outputPath);
            var tempDir = Path.GetDirectoryName(outputPath);
            if (tempDir is not null
                && Directory.Exists(tempDir)
                && !Directory.EnumerateFileSystemEntries(tempDir).Any())
            {
                Directory.Delete(tempDir);
            }
        }
        catch (Exception ex)
        {
            ReportProgress($"  Warning: Cleanup issue — {ex.Message}");
        }
    }

    private void ReportProgress(string message)
    {
        ProgressChanged?.Invoke(message);
    }
}

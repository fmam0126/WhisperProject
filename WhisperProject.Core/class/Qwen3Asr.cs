using SharpCompress.Archives;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SherpaOnnx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using WhisperProject.Class;

namespace WhisperProject.Core
{
    public class Qwen3Asr
    {
        /// <summary>
        /// Folder name the sherpa-onnx-whisper-tiny archive extracts into, relative to the model directory.
        /// </summary>
        private const string WhisperTinyModelDirName = "sherpa-onnx-whisper-tiny";

        private sealed record DownloadProgress(double Fraction, double MegabytesPerSecond);

        /// <summary>
        /// Builds the local path to a file inside the extracted sherpa-onnx-whisper-tiny
        /// model folder, relative to the given model directory.
        /// </summary>
        internal static string GetWhisperTinyModelPath(string modelDir, string fileName) =>
            Path.Combine(modelDir, WhisperTinyModelDirName, fileName);

        /// <summary>
        /// Runs the Qwen3 ASR model on the specified audio file using the given model directory.
        /// </summary>
        /// <param name="audioFilePath">The path to the audio file.</param>
        /// <param name="modelDir">The directory containing the model files.</param>
        /// <returns>the detected spoken language</returns>
        public static async Task<string> RunQwen3Asr(string audioFilePath, string modelDir)
        {
            // https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-qwen3-asr-0.6B-int8-2026-03-25.tar.bz2
            // please download model files from
            // https://github.com/k2-fsa/sherpa-onnx/releases/tag/asr-models
            var config = new OfflineRecognizerConfig();
            if (audioFilePath == null)
            {
                throw new ArgumentNullException(nameof(audioFilePath));
            }
            if (modelDir == null)
            {
                throw new ArgumentNullException(nameof(modelDir));
            }
            if (!File.Exists(Path.Combine(modelDir, "qwen3", "conv_frontend.onnx")) ||
                !File.Exists(Path.Combine(modelDir, "qwen3", "encoder.int8.onnx")) ||
                !File.Exists(Path.Combine(modelDir, "qwen3", "decoder.int8.onnx")) ||
                !Directory.Exists(Path.Combine(modelDir, "qwen3", "tokenizer")))
            {
                if (File.Exists($"{modelDir}/qwen3asr.tar.bz2"))
                {
                    Console.WriteLine("Model archive already exists. Skipping download.");
                }
                else
                {
                    Console.WriteLine("Downloading model files...");
                    var progress = new Progress<DownloadProgress>(status =>
                    {
                        var percentage = status.Fraction >= 0
                            ? $"{status.Fraction:P2}"
                            : "unknown";
                        var message = $"Download progress: {percentage} | {status.MegabytesPerSecond:F2} MB/s";
                        Console.Write($"\r{message.PadRight(40)}");

                        if (status.Fraction >= 1)
                        {
                            Console.WriteLine();
                        }
                    });
                    await DownloadModel("https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-qwen3-asr-0.6B-int8-2026-03-25.tar.bz2", Path.Combine(modelDir, "qwen3asr.tar.bz2"), progress);
                }
                
                //Console.WriteLine("Extracting model files...");
                await ExtractTarBz2(Path.Combine(modelDir, "qwen3asr.tar.bz2"), modelDir, "qwen3");
            }

            config.ModelConfig.Qwen3Asr.ConvFrontend = Path.Combine(modelDir, "qwen3", "conv_frontend.onnx");
            config.ModelConfig.Qwen3Asr.Encoder = Path.Combine(modelDir, "qwen3", "encoder.int8.onnx");
            config.ModelConfig.Qwen3Asr.Decoder = Path.Combine(modelDir, "qwen3", "decoder.int8.onnx");
            config.ModelConfig.Qwen3Asr.Tokenizer = Path.Combine(modelDir, "qwen3", "tokenizer");
            config.ModelConfig.Qwen3Asr.Hotwords = "";
            config.ModelConfig.Tokens = "";
            config.ModelConfig.Debug = 0;
            var recognizer = new OfflineRecognizer(config);

            var vadModelConfig = new VadModelConfig();
            if (!File.Exists(Path.Combine(modelDir, "silero_vad.onnx")) && !File.Exists(Path.Combine(modelDir, "ten-vad.onnx")))
            {
                try
                {
                    var progress = new Progress<DownloadProgress>(status =>
                    {
                        var percentage = status.Fraction >= 0
                            ? $"{status.Fraction:P2}"
                            : "unknown";
                        var message = $"Download progress: {percentage} | {status.MegabytesPerSecond:F2} MB/s";
                        Console.Write($"\r{message.PadRight(40)}");

                        if (status.Fraction >= 1)
                        {
                            Console.WriteLine();
                        }
                    });
                    await DownloadModel("https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/silero_vad_v5.onnx", Path.Combine(modelDir, "silero_vad.onnx"), progress);
                }
                catch (Exception ex)
                {
                    throw new FileNotFoundException("Failed to download VAD model.", ex);
                }
            }
            if (File.Exists(Path.Combine(modelDir, "silero_vad.onnx")))
            {
                Console.WriteLine("Use silero-vad");
                vadModelConfig.SileroVad.Model = Path.Combine(modelDir, "silero_vad.onnx");
                vadModelConfig.SileroVad.Threshold = 0.3F;
                vadModelConfig.SileroVad.MinSilenceDuration = 0.5F;
                vadModelConfig.SileroVad.MinSpeechDuration = 0.25F;
                vadModelConfig.SileroVad.MaxSpeechDuration = 5.0F;
                vadModelConfig.SileroVad.WindowSize = 512;
            }
            else if (File.Exists(Path.Combine(modelDir, "ten-vad.onnx")))
            {
                Console.WriteLine("Use ten-vad");
                vadModelConfig.TenVad.Model = Path.Combine(modelDir, "ten-vad.onnx");
                vadModelConfig.TenVad.Threshold = 0.3F;
                vadModelConfig.TenVad.MinSilenceDuration = 0.5F;
                vadModelConfig.TenVad.MinSpeechDuration = 0.25F;
                vadModelConfig.TenVad.MaxSpeechDuration = 5.0F;
                vadModelConfig.TenVad.WindowSize = 256;
            }
            else
            {
                throw new FileNotFoundException("No VAD model found in the specified model directory.");
            }
            vadModelConfig.Debug = 0;

            var vad = new VoiceActivityDetector(vadModelConfig, 60);

            var reader = new WaveReader(audioFilePath);

            int numSamples = reader.Samples.Length;
            int windowSize = vadModelConfig.SileroVad.WindowSize;

            if (vadModelConfig.TenVad.Model != "")
            {
                windowSize = vadModelConfig.TenVad.WindowSize;
            }

            int sampleRate = vadModelConfig.SampleRate;
            int numIter = numSamples / windowSize;
            var srtPath = Path.ChangeExtension(audioFilePath, ".srt");
            int segmentIndex = 1;
            using var writer = new StreamWriter(srtPath);
            for (int i = 0; i != numIter; ++i)
            {
                int start = i * windowSize;
                var samples = new float[windowSize];
                Array.Copy(reader.Samples, start, samples, 0, windowSize);
                vad.AcceptWaveform(samples);
                if (vad.IsSpeechDetected())
                {
                    while (!vad.IsEmpty())
                    {
                        SpeechSegment segment = vad.Front();
                        var startTime = segment.Start / (float)sampleRate;
                        var duration = segment.Samples.Length / (float)sampleRate;

                        OfflineStream stream = recognizer.CreateStream();
                        stream.AcceptWaveform(sampleRate, segment.Samples);
                        recognizer.Decode(stream);
                        var text = stream.Result.Text;

                        if (!string.IsNullOrEmpty(text))
                        {
                            Console.WriteLine($"{WhisperClient.FormatSrtTime(TimeSpan.FromSeconds(startTime))}-->{WhisperClient.FormatSrtTime(TimeSpan.FromSeconds(startTime + duration))}: {text}");
                            await writer.WriteLineAsync(segmentIndex.ToString());
                            await writer.WriteLineAsync($"{WhisperClient.FormatSrtTime(TimeSpan.FromSeconds(startTime))}-->{WhisperClient.FormatSrtTime(TimeSpan.FromSeconds(startTime + duration))}");
                            await writer.WriteLineAsync(text.Trim());
                            await writer.WriteLineAsync();
                            segmentIndex++;
                        }

                        vad.Pop();
                    }
                }
            }

            vad.Flush();

            while (!vad.IsEmpty())
            {
                var segment = vad.Front();
                float startTime = segment.Start / (float)sampleRate;
                float duration = segment.Samples.Length / (float)sampleRate;

                var stream = recognizer.CreateStream();
                stream.AcceptWaveform(sampleRate, segment.Samples);
                recognizer.Decode(stream);
                
                var text = stream.Result.Text;

                if (!string.IsNullOrEmpty(text))
                {
                    Console.WriteLine($"{WhisperClient.FormatSrtTime(TimeSpan.FromSeconds(startTime))}-->{WhisperClient.FormatSrtTime(TimeSpan.FromSeconds(startTime + duration))}: {text}");
                    await writer.WriteLineAsync(segmentIndex.ToString());
                    await writer.WriteLineAsync($"{WhisperClient.FormatSrtTime(TimeSpan.FromSeconds(startTime))}-->{WhisperClient.FormatSrtTime(TimeSpan.FromSeconds(startTime + duration))}");
                    await writer.WriteLineAsync(text.Trim());
                    await writer.WriteLineAsync();
                    segmentIndex++;
                }

                vad.Pop();
            }
            await writer.DisposeAsync();
            return await DetectSpokenLanguage(audioFilePath, modelDir);
        }
        /// <summary>
        /// runs the spoken language detection model on a test wave file using the specified model directory.
        /// </summary>
        /// <param name="audioFilePath">The path to the audio file.</param>
        /// <param name="modelDir">The path to the model directory.</param>
        /// <returns>The detected spoken language.</returns>
        /// <exception cref="ArgumentException"></exception>
        private static async Task<string> DetectSpokenLanguage(string audioFilePath, string modelDir)
        {
            // https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-whisper-tiny.tar.bz2
            var modelUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-whisper-tiny.tar.bz2";
            var config = new SpokenLanguageIdentificationConfig();
            if (string.IsNullOrEmpty(modelDir))
            {
                throw new ArgumentException("Model directory path is null or empty.", nameof(modelDir));
            }
            config.Whisper.Encoder = GetWhisperTinyModelPath(modelDir, "tiny-encoder.int8.onnx");
            config.Whisper.Decoder = GetWhisperTinyModelPath(modelDir, "tiny-decoder.int8.onnx");
            if (!File.Exists(config.Whisper.Encoder) || !File.Exists(config.Whisper.Decoder))
            {
                if (File.Exists(Path.Combine(modelDir, "sherpa-onnx-whisper-tiny.tar.bz2")))
                {
                    Console.WriteLine("Model archive already exists. Skipping download.");
                }
                else
                {
                    Console.WriteLine("Downloading model files...");
                    var progress = new Progress<DownloadProgress>(status =>
                    {
                        var percentage = status.Fraction >= 0
                            ? $"{status.Fraction:P2}"
                            : "unknown";
                        var message = $"Download progress: {percentage} | {status.MegabytesPerSecond:F2} MB/s";
                        Console.Write($"\r{message.PadRight(40)}");

                        if (status.Fraction >= 1)
                        {
                            Console.WriteLine();
                        }
                    });
                    await DownloadModel(modelUrl, Path.Combine(modelDir, "sherpa-onnx-whisper-tiny.tar.bz2"), progress);
                }

                await ExtractTarBz2(Path.Combine(modelDir, "sherpa-onnx-whisper-tiny.tar.bz2"), modelDir, WhisperTinyModelDirName);
            }


            var slid = new SpokenLanguageIdentification(config);
            

            var waveReader = new WaveReader(audioFilePath);

            var s = slid.CreateStream();
            s.AcceptWaveform(waveReader.SampleRate, waveReader.Samples);
            var result = slid.Compute(s);
            Console.WriteLine($"Filename: {audioFilePath}");
            Console.WriteLine($"Detected language: {result.Lang}");
            return result.Lang;
        }
        private static async Task DownloadModel(
            string modelUrl,
            string destinationPath,
            IProgress<DownloadProgress>? progress = null)
        {
            Console.WriteLine("Downloading model from: " + modelUrl);

            try
            {
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(8)
                };

                using var response = await client.GetAsync(
                    modelUrl,
                    HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;
                var downloadedBytes = 0L;
                var buffer = new byte[81920];
            var stopwatch = Stopwatch.StartNew();

            progress?.Report(new DownloadProgress(0, 0));

                await using var downloadStream = await response.Content.ReadAsStreamAsync();
                await using var fileWriter = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: buffer.Length,
                    useAsync: true);

                int bytesRead;
                while ((bytesRead = await downloadStream.ReadAsync(buffer)) > 0)
                {
                    await fileWriter.WriteAsync(buffer.AsMemory(0, bytesRead));

                    downloadedBytes += bytesRead;

                    var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                    var megabytesPerSecond = elapsedSeconds > 0
                        ? downloadedBytes / 1024d / 1024d / elapsedSeconds
                        : 0;

                    var fraction = totalBytes.HasValue && totalBytes.Value > 0
                        ? (double)downloadedBytes / totalBytes.Value
                        : -1;

                    progress?.Report(new DownloadProgress(
                        fraction,
                        megabytesPerSecond));
                }

                var finalElapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                var finalSpeed = finalElapsedSeconds > 0
                    ? downloadedBytes / 1024d / 1024d / finalElapsedSeconds
                    : 0;
                progress?.Report(new DownloadProgress(1, finalSpeed));

                Console.WriteLine(
                    $"Model downloaded successfully to {destinationPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading model: {ex.Message}");
                throw;
            }
        }
        private static async Task ExtractTarBz2(string tarBz2FilePath, string outputDirectory, string subFolderName)
        {
            Console.WriteLine("Extracting model files from: " + tarBz2FilePath);
            try
            {
                using var fileStream = File.OpenRead(tarBz2FilePath);
                await using var archiveReader = await ReaderFactory.OpenAsyncReader(fileStream, cancellationToken: default);

                // Resolve absolute path for base directory and append separator to prevent prefix aliasing
                string targetBaseDir = Path.GetFullPath(Path.Combine(outputDirectory, subFolderName ?? string.Empty));
                if (!targetBaseDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    targetBaseDir += Path.DirectorySeparatorChar;
                }

                while (await archiveReader.MoveToNextEntryAsync(cancellationToken: default))
                {
                    if (!archiveReader.Entry.IsDirectory)
                    {
                        string rawKey = archiveReader.Entry.Key?.Replace('\\', '/') ?? string.Empty;
                        int firstSlashIndex = rawKey.IndexOf('/');
                        string relativePath = firstSlashIndex >= 0 ? rawKey.Substring(firstSlashIndex + 1) : rawKey;

                        // Reject empty paths, rooted paths, or directory traversal segments ('.' or '..')
                        if (string.IsNullOrWhiteSpace(relativePath) ||
                            Path.IsPathRooted(relativePath) ||
                            relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                         .Any(segment => segment == "." || segment == ".."))
                        {
                            throw new InvalidOperationException($"Archive entry '{archiveReader.Entry.Key}' contains invalid or unsafe path.");
                        }

                        string fullOutputPath = Path.GetFullPath(Path.Combine(targetBaseDir, relativePath));

                        // Verify the output path remains within the intended target directory
                        if (!fullOutputPath.StartsWith(targetBaseDir, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException($"Archive entry '{archiveReader.Entry.Key}' would escape target directory.");
                        }

                        // Ensure target directory exists before file creation
                        string? destinationDirectory = Path.GetDirectoryName(fullOutputPath);
                        if (!string.IsNullOrEmpty(destinationDirectory))
                        {
                            Directory.CreateDirectory(destinationDirectory);
                        }

                        using var outputStream = File.Create(fullOutputPath);
                        await archiveReader.WriteEntryToAsync(outputStream, cancellationToken: default);
                    }
                }

                Console.WriteLine($"Extracted {tarBz2FilePath} to {targetBaseDir}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting {tarBz2FilePath}: {ex.Message}");
                throw;
            }
        }
    }
}

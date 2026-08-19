using System;
using System.IO;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Logger;
using System.Globalization;
using Whisper.net.Wave;
using WhisperProject.Core;
using WhisperProject.Models;


namespace WhisperProject.Class;

public static class WhisperClient
{
    private const string WhisperModelUrl =
        "https://huggingface.co/sandrohanea/whisper.net/resolve/v4/classic/ggml-large-v2.bin";
    private const string WhisperVadModelUrl =
        "https://huggingface.co/sandrohanea/whisper.net/resolve/v4/vad/ggml-silero-v6.2.0.bin";

    public static string FormatSrtTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;

        int totalHours = (int)time.TotalHours;
        return $"{totalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2},{time.Milliseconds:D3}";
    }

    /// <summary>
    /// transcribes the given audio file using the Whisper model and writes the results to an SRT file. If the model file does not exist, it will be downloaded automatically. 
    /// The language can be specified, or set to "auto" for automatic detection.
    /// </summary>
    /// <param name="inputFileName">The path to the input audio file</param>
    /// <param name="modelPath">The path to the Whisper model file</param>
    /// <param name="progress">Optional progress reporter for the model download.</param>
    /// <returns>The language identified by Whisper, or null if no segments were produced.</returns>
    public static async Task<string?> TranscribeAsync(string inputFileName, string? modelPath = null, string? language = null, IProgress<DownloadProgress>? progress = null)
    {
        var modelFileName = string.IsNullOrWhiteSpace(modelPath) ? "ggml-largev2.bin" : modelPath;
        var wavFileName = inputFileName;

        using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);

        if (!File.Exists(modelFileName))
        {
            Console.WriteLine($"Downloading Model {modelFileName}");
            await ModelDownloader.DownloadAsync(WhisperModelUrl, modelFileName, progress);
        }

        using var whisperFactory = WhisperFactory.FromPath(modelFileName);

        var builder = whisperFactory.CreateBuilder()
            .WithThreads(Environment.ProcessorCount)
            .WithLanguage(string.IsNullOrWhiteSpace(language) ? "auto" : language);

        using var processor = builder.Build();

        using var fileStream = File.OpenRead(wavFileName);

        var segments = new List<SegmentData>();
        string? identifiedLanguage = null;

        try
        {
            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                identifiedLanguage = result.Language;
                segments.Add(result);
                Console.WriteLine($"{result.Start}->{result.End}: {result.Text}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Transcription interrupted: {ex}");
        }

        if (segments.Count == 0)
        {
            Console.WriteLine("No subtitles were produced. Skipping SRT write.");
            return identifiedLanguage;
        }

        var srtPath = Path.ChangeExtension(inputFileName, ".srt");
        try
        {
            using var writer = new StreamWriter(srtPath);

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                await writer.WriteLineAsync((i + 1).ToString());
                await writer.WriteLineAsync($"{FormatSrtTime(seg.Start)} --> {FormatSrtTime(seg.End)}");
                await writer.WriteLineAsync(seg.Text.Trim());
                await writer.WriteLineAsync();
            }

            Console.WriteLine($"Transcription complete: {segments.Count} subtitle entries written to {srtPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write SRT file: {ex}");
        }

        return identifiedLanguage;
    }

    /// <summary>
    /// Transcribes the given audio file using the Whisper model with Voice Activity Detection (VAD) and writes the results to an SRT file. 
    /// If the model file does not exist, it will be downloaded automatically. The language can be specified, or set to "auto" for automatic detection.
    /// </summary>
    /// <param name="inputFileName">The path to the input audio file</param>
    /// <param name="modelPath">The path to the Whisper model file</param>
    /// <param name="language">The language of the audio file</param>
    /// <param name="progress">Optional progress reporter for the model downloads.</param>
    /// <returns>The language identified by Whisper, or null if no speech was detected.</returns>
    public static async Task<string?> TranscribeVadAsync(string inputFileName, string? modelPath = null, string? language = null, IProgress<DownloadProgress>? progress = null)
    {
        var modelFileName = string.IsNullOrWhiteSpace(modelPath) ? "ggml-largev2.bin" : modelPath;
        var vadModelFileName = "./ggml-silero-v6.2.0.bin";
        var wavFileName = inputFileName;
        const int sampleRate = 16000;

        using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);

        if (!File.Exists(modelFileName))
        {
            Console.WriteLine($"Downloading Model {modelFileName}");
            await ModelDownloader.DownloadAsync(WhisperModelUrl, modelFileName, progress);
        }
        if (!File.Exists(vadModelFileName))
        {
            Console.WriteLine($"Downloading VAD Model {vadModelFileName}");
            await ModelDownloader.DownloadAsync(WhisperVadModelUrl, vadModelFileName, progress);
        }

        using var fileStream = File.OpenRead(wavFileName);
        var waveParser = new WaveParser(fileStream);
        var samples = await waveParser.GetAvgSamplesAsync();
        Console.WriteLine($"Loaded {samples.Length} samples ({samples.Length / (double)sampleRate:F1}s of audio)");

        using var whisperVadFactory = WhisperVadFactory.FromPath(vadModelFileName);
        using var vadProcessor = whisperVadFactory.CreateBuilder()
            .WithThreads(Environment.ProcessorCount)
            .WithUseGpu(false)
            .WithThreshold(0.5f)
            .WithMaxSpeechDuration(TimeSpan.FromSeconds(30))
            .WithMinSpeechDuration(TimeSpan.FromMilliseconds(250))
            .WithMinSilenceDuration(TimeSpan.FromMilliseconds(300))
            .WithSamplesOverlap(TimeSpan.FromMilliseconds(100))
            .WithSpeechPadding(TimeSpan.FromMilliseconds(300))
            .Build();

        var vadSegments = await vadProcessor.DetectSpeechAsync(samples);

        Console.WriteLine($"VAD found {vadSegments.Count} speech segment(s):");
        foreach (var seg in vadSegments)
        {
            Console.WriteLine($"  {seg.Start} -> {seg.End} ({seg.End - seg.Start})");
        }

        if (vadSegments.Count == 0)
        {
            Console.WriteLine("No speech detected. Skipping transcription.");
            return null;
        }

        const double silenceLenSec = 0.1;
        int silenceSamples = (int)(silenceLenSec * sampleRate);
        var mappingTable = new List<VadTimeMapping>();

        int filteredSampleCount = 0;
        for (int i = 0; i < vadSegments.Count; i++)
        {
            int startSample = (int)(vadSegments[i].Start.TotalSeconds * sampleRate);
            int endSample = (int)(vadSegments[i].End.TotalSeconds * sampleRate);
            startSample = Math.Clamp(startSample, 0, samples.Length - 1);
            endSample = Math.Clamp(endSample, 0, samples.Length - 1);
            filteredSampleCount += (endSample - startSample);
        }
        int totalSilenceSamples = (vadSegments.Count > 1) ? (vadSegments.Count - 1) * silenceSamples : 0;
        int totalSamplesNeeded = filteredSampleCount + totalSilenceSamples;

        var filteredSamples = new float[totalSamplesNeeded];

        int offset = 0;
        for (int i = 0; i < vadSegments.Count; i++)
        {
            int segmentStart = (int)(vadSegments[i].Start.TotalSeconds * sampleRate);
            int segmentEnd = (int)(vadSegments[i].End.TotalSeconds * sampleRate);
            segmentStart = Math.Clamp(segmentStart, 0, samples.Length - 1);
            segmentEnd = Math.Clamp(segmentEnd, 0, samples.Length - 1);

            int originalLen = segmentEnd - segmentStart;

            if (originalLen > 0)
            {
                long origStart = vadSegments[i].Start.Ticks;
                long origEnd = vadSegments[i].End.Ticks;
                long vadStart = (long)(offset / (double)sampleRate * TimeSpan.TicksPerSecond);
                long vadEnd = (long)((offset + originalLen) / (double)sampleRate * TimeSpan.TicksPerSecond);

                mappingTable.Add(new VadTimeMapping(vadStart, origStart));
                mappingTable.Add(new VadTimeMapping(vadEnd, origEnd));

                Array.Copy(samples, segmentStart, filteredSamples, offset, originalLen);
                offset += originalLen;

                if (i < vadSegments.Count - 1)
                {
                    long silStart = (long)(offset / (double)sampleRate * TimeSpan.TicksPerSecond);
                    long silEnd = (long)((offset + silenceSamples) / (double)sampleRate * TimeSpan.TicksPerSecond);
                    long origSilStart = vadSegments[i].End.Ticks;
                    long origSilEnd = vadSegments[i + 1].Start.Ticks;

                    mappingTable.Add(new VadTimeMapping(silStart, origSilStart));
                    mappingTable.Add(new VadTimeMapping(silEnd, origSilEnd));

                    Array.Clear(filteredSamples, offset, silenceSamples);
                    offset += silenceSamples;
                }
            }
        }

        mappingTable.Sort((a, b) => a.ProcessedTimeTicks.CompareTo(b.ProcessedTimeTicks));
        for (int i = mappingTable.Count - 1; i > 0; i--)
        {
            if (mappingTable[i].ProcessedTimeTicks == mappingTable[i - 1].ProcessedTimeTicks)
            {
                mappingTable.RemoveAt(i);
            }
        }

        Console.WriteLine($"Filtered audio: {samples.Length} -> {offset} samples ({100.0f * (1.0f - (float)offset / samples.Length):F1}% reduction)");
        Console.WriteLine($"Time mapping table has {mappingTable.Count} points");

        using var whisperFactory = WhisperFactory.FromPath(modelFileName);
        using var processor = whisperFactory.CreateBuilder()
            .WithThreads(Environment.ProcessorCount)
            .WithLanguage(string.IsNullOrWhiteSpace(language) ? "auto" : language)
            .Build();

        var subtitles = new List<(TimeSpan Start, TimeSpan End, string Text)>();
        string? identifiedLanguage = null;
        try
        {
            await foreach (var result in processor.ProcessAsync(filteredSamples.AsMemory(0, offset)))
            {
                identifiedLanguage = result.Language;
                var originalStart = MapToOriginalTime(result.Start.Ticks, mappingTable);
                var originalEnd = MapToOriginalTime(result.End.Ticks, mappingTable);
                Console.WriteLine($"{originalStart}->{originalEnd}: {result.Text}");
                subtitles.Add((originalStart, originalEnd, result.Text));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Transcription interrupted: {ex}");
        }

        if (subtitles.Count == 0)
        {
            Console.WriteLine("No subtitles were produced. Skipping SRT write.");
            return identifiedLanguage;
        }

        var srtPath = Path.ChangeExtension(inputFileName, ".srt");
        try
        {
            using var writer = new StreamWriter(srtPath);

            for (int i = 0; i < subtitles.Count; i++)
            {
                var (start, end, text) = subtitles[i];
                await writer.WriteLineAsync((i + 1).ToString());
                await writer.WriteLineAsync($"{FormatSrtTime(start)} --> {FormatSrtTime(end)}");
                await writer.WriteLineAsync(text.Trim());
                await writer.WriteLineAsync();
            }

            Console.WriteLine($"Transcription complete: {subtitles.Count} subtitle entries written to {srtPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write SRT file: {ex}");
        }

        return identifiedLanguage;
    }
    /// <summary>
    /// Maps a processed time (after VAD filtering) back to the original time in the audio file using a mapping table.
    /// </summary>
    /// <param name="processedTimeTicks">The processed time in ticks</param>
    /// <param name="mappingTable">The mapping table</param>
    /// <returns>The original time in ticks</returns>
    internal static TimeSpan MapToOriginalTime(long processedTimeTicks, List<VadTimeMapping> mappingTable)
    {
        if (mappingTable.Count == 0)
            return TimeSpan.FromTicks(processedTimeTicks);

        int idx = mappingTable.BinarySearch(
            new VadTimeMapping(processedTimeTicks, 0),
            VadTimeMapping.ProcessedTimeComparer);

        if (idx >= 0)
            return TimeSpan.FromTicks(mappingTable[idx].OriginalTimeTicks);

        idx = ~idx;

        if (idx == 0)
        {
            var m = mappingTable[0];
            if (m.ProcessedTimeTicks == 0)
                return TimeSpan.FromTicks(0);
            double ratio = (double)processedTimeTicks / m.ProcessedTimeTicks;
            return TimeSpan.FromTicks((long)(m.OriginalTimeTicks * ratio));
        }

        if (idx >= mappingTable.Count)
        {
            var m = mappingTable[mappingTable.Count - 1];
            long delta = processedTimeTicks - m.ProcessedTimeTicks;
            return TimeSpan.FromTicks(m.OriginalTimeTicks + delta);
        }

        var prev = mappingTable[idx - 1];
        var next = mappingTable[idx];

        if (next.ProcessedTimeTicks == prev.ProcessedTimeTicks)
            return TimeSpan.FromTicks(prev.OriginalTimeTicks);

        double t = (double)(processedTimeTicks - prev.ProcessedTimeTicks)
                   / (next.ProcessedTimeTicks - prev.ProcessedTimeTicks);
        long origTicks = (long)(prev.OriginalTimeTicks + t * (next.OriginalTimeTicks - prev.OriginalTimeTicks));
        return TimeSpan.FromTicks(origTicks);
    }

    internal record VadTimeMapping(long ProcessedTimeTicks, long OriginalTimeTicks)
    {
        public static readonly IComparer<VadTimeMapping> ProcessedTimeComparer =
            Comparer<VadTimeMapping>.Create((a, b) => a.ProcessedTimeTicks.CompareTo(b.ProcessedTimeTicks));
    }
}
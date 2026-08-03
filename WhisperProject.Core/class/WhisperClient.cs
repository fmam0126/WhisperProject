using System;
using System.IO;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Logger;
using System.Globalization;
using Whisper.net.Wave;


namespace WhisperProject.Class;

public static class WhisperClient
{
    public static string IdentifiedLanguage { get; private set; } = "gamer";
    public static string FormatSrtTime(TimeSpan time)
    {
        return time.ToString(@"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture);
    }

    // This examples shows how to use Whisper.net to create a transcription from an audio file with 16Khz sample rate.
    // It uses both Cuda (NVidia GPU) or CPU, and loads the first one that is available.
    public static async Task TranscribeAsync(string inputFileName, string? modelPath = null, string? language = null)
    {
        // We declare three variables which we will use later, ggmlType, modelFileName and wavFileName
        var ggmlType = GgmlType.LargeV2;
        var modelFileName = string.IsNullOrWhiteSpace(modelPath) ? "ggml-largev2.bin" : modelPath;
        // var vadModelFileName = "./ggml-silero-v6.2.0.bin";
        var wavFileName = inputFileName;

        using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);

        // This section detects whether the "ggml-largev3.bin" file exists in our project disk. If it doesn't, it downloads it from the internet
        if (!File.Exists(modelFileName))
        {
            await DownloadModel(modelFileName, ggmlType);
        }

        // This section creates the whisperFactory object which is used to create the processor object.
        using var whisperFactory = WhisperFactory.FromPath(modelFileName);

        // This section creates the processor object which is used to process the audio file, it uses language `auto` to detect the language of the audio file.
        var builder = whisperFactory.CreateBuilder()
            .WithThreads(Environment.ProcessorCount)
            .WithLanguage(string.IsNullOrWhiteSpace(language) ? "auto" : language);


        using var processor = builder.Build();



        using var fileStream = File.OpenRead(wavFileName);
        // using var writer = new StreamWriter($"{Path.GetDirectoryName(wavFileName)}\\{Path.GetFileNameWithoutExtension(wavFileName)}.srt");
        // int index = 1;

        var results = new List<object>();
        // This section processes the audio file and prints the results (start time, end time and text) to the console.
        // await foreach (var result in processor.ProcessAsync(fileStream))
        // {
        //     results.Add(result);
        //     Console.WriteLine($"{result.Start}->{result.End}: {result.Text}");
        //     IdentifiedLanguage = result.Language;
        //     await writer.WriteLineAsync(index.ToString());
        //     await writer.WriteLineAsync($"{FormatSrtTime(result.Start)} --> {FormatSrtTime(result.End)}");
        //     await writer.WriteLineAsync(result.Text.Trim());
        //     await writer.WriteLineAsync();

        //     index++;
        // }


        var segments = new List<SegmentData>();

        try
        {
            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                IdentifiedLanguage = result.Language;
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
            return;
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
    }


    public static async Task TranscribeVadAsync(string inputFileName, string? modelPath = null, string? language = null)
    {
        var ggmlType = GgmlType.LargeV2;
        var modelFileName = string.IsNullOrWhiteSpace(modelPath) ? "ggml-largev2.bin" : modelPath;
        var vadModelFileName = "./ggml-silero-v6.2.0.bin";
        var sileroVadType = SileroVadType.V6_2_0;
        var wavFileName = inputFileName;
        const int sampleRate = 16000;

        using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);

        if (!File.Exists(modelFileName))
        {
            await DownloadModel(modelFileName, ggmlType);
        }
        if (!File.Exists(vadModelFileName))
        {
            await DownloadVadModel(vadModelFileName, sileroVadType);
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
            return;
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
        try
        {
            await foreach (var result in processor.ProcessAsync(filteredSamples.AsMemory(0, offset)))
            {
                IdentifiedLanguage = result.Language;
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
            return;
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
    }

    private static TimeSpan MapToOriginalTime(long processedTimeTicks, List<VadTimeMapping> mappingTable)
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

    private record VadTimeMapping(long ProcessedTimeTicks, long OriginalTimeTicks)
    {
        public static readonly IComparer<VadTimeMapping> ProcessedTimeComparer =
            Comparer<VadTimeMapping>.Create((a, b) => a.ProcessedTimeTicks.CompareTo(b.ProcessedTimeTicks));
    }

    private static async Task DownloadModel(string fileName, GgmlType ggmlType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Model file name must not be empty.", nameof(fileName));
        Console.WriteLine($"Downloading Model {fileName}");
        using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
        using var fileWriter = File.OpenWrite(fileName);
        await modelStream.CopyToAsync(fileWriter);
    }
    private static async Task DownloadVadModel(string fileName, SileroVadType vadType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("VAD model file name must not be empty.", nameof(fileName));
        Console.WriteLine($"Downloading VAD Model {fileName}");
        using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlSileroVadModelAsync(vadType);
        using var fileWriter = File.OpenWrite(fileName);
        await modelStream.CopyToAsync(fileWriter);
    }


}
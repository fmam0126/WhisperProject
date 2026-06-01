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
    public static async Task TranscribeAsync(string inputFileName, string? modelPath = null, string? language = null, bool useVad = false)
    {
        // We declare three variables which we will use later, ggmlType, modelFileName and wavFileName
        var ggmlType = GgmlType.LargeV2;
        var modelFileName = modelPath ?? "ggml-largev2.bin";
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
            .WithLanguage(language ?? "auto");

        if (useVad)
        {
            // builder.WithVad(vadModelFileName)
            //     .WithVadThreshold(0.5f)
            //     .WithVadMinSpeechDurationMs(250)
            //     .WithVadMaxSpeechDurationS(30f)
            //     .WithVadSpeechPadMs(30)
            //     .WithVadSamplesOverlap(0.1f);

        }

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

        await foreach (var result in processor.ProcessAsync(fileStream))
        {
            IdentifiedLanguage = result.Language;
            segments.Add(result);
            // Optional: Minimal logging so you know it's working
            // Console.WriteLine($"Transcribing: {result.Start:mm\\:ss}" + result.Text);
            Console.WriteLine($"{result.Start}->{result.End}: {result.Text}");
        }

        // 3. Write the SRT file all at once at the end
        var srtPath = Path.ChangeExtension(inputFileName, ".srt");
        using var writer = new StreamWriter(srtPath);

        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            await writer.WriteLineAsync((i + 1).ToString());
            await writer.WriteLineAsync($"{FormatSrtTime(seg.Start)} --> {FormatSrtTime(seg.End)}");
            await writer.WriteLineAsync(seg.Text.Trim());
            await writer.WriteLineAsync();
        }
    }


    // this is dumb. redo.
    public static async Task TranscribeVadAsync(string inputFileName, string? modelPath = null, string? language = null)
    {
        var ggmlType = GgmlType.LargeV2;
        var modelFileName = modelPath ?? "ggml-largev2.bin";
        var vadModelFileName = "./ggml-silero-v6.2.0.bin";
        var wavFileName = inputFileName;
        const int sampleRate = 16000; // Whisper requires 16kHz

        using var whisperLogger = LogProvider.AddConsoleLogging(WhisperLogLevel.Debug);

        // Download models if missing
        if (!File.Exists(modelFileName))
        {
            await DownloadModel(modelFileName, ggmlType);
        }

        // ── Step 1: Parse the WAV file and extract raw float samples ──
        using var fileStream = File.OpenRead(wavFileName);
        var waveParser = new WaveParser(fileStream);
        var samples = await waveParser.GetAvgSamplesAsync();
        Console.WriteLine($"Loaded {samples.Length} samples ({samples.Length / (double)sampleRate:F1}s of audio)");

        // ── Step 2: Run VAD to detect speech segments ──
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

        // ── Step 3: Transcribe each VAD segment individually ──
        using var whisperFactory = WhisperFactory.FromPath(modelFileName);
        using var processor = whisperFactory.CreateBuilder()
            .WithThreads(Environment.ProcessorCount)
            .WithLanguage(language ?? "auto")
            .Build();

        var allSegments = new List<SegmentData>();
        int srtIndex = 1;

        var srtPath = Path.ChangeExtension(inputFileName, ".srt");
        using var writer = new StreamWriter(srtPath);

        foreach (var vadSegment in vadSegments)
        {
            // Convert TimeSpan to sample indices
            int startSample = (int)(vadSegment.Start.TotalSeconds * sampleRate);
            int endSample = (int)(vadSegment.End.TotalSeconds * sampleRate);

            // Clamp to valid range
            startSample = Math.Max(0, startSample);
            endSample = Math.Min(samples.Length, endSample);

            if (endSample <= startSample) continue;

            // Slice the samples for this VAD segment
            var slice = samples.AsMemory(startSample, endSample - startSample);

            Console.WriteLine($"Transcribing segment {vadSegment.Start} -> {vadSegment.End}...");

            // Transcribe the slice – timestamps will be relative to slice start
            await foreach (var seg in processor.ProcessAsync(slice))
            {
                IdentifiedLanguage = seg.Language;

                // Offset timestamps by the VAD segment's start time
                var absoluteStart = vadSegment.Start + seg.Start;
                var absoluteEnd = vadSegment.Start + seg.End;

                Console.WriteLine($"  {absoluteStart}->{absoluteEnd}: {seg.Text}");

                // Write directly to SRT
                await writer.WriteLineAsync(srtIndex.ToString());
                await writer.WriteLineAsync($"{FormatSrtTime(absoluteStart)} --> {FormatSrtTime(absoluteEnd)}");
                await writer.WriteLineAsync(seg.Text.Trim());
                await writer.WriteLineAsync();

                srtIndex++;
                allSegments.Add(seg);
            }
        }

        Console.WriteLine($"Transcription complete: {srtIndex - 1} subtitle entries written to {srtPath}");
    }

    private static async Task DownloadModel(string fileName, GgmlType ggmlType)
    {
        Console.WriteLine($"Downloading Model {fileName}");
        using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
        using var fileWriter = File.OpenWrite(fileName);
        await modelStream.CopyToAsync(fileWriter);
    }


}
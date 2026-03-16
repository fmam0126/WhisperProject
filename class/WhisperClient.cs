using System;
using System.IO;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Logger;
using System.Globalization;
public static class WhisperClient
{
    public static string IdentifiedLanguage {get; private set;} = "gamer";
    public static string FormatSrtTime(TimeSpan time)
    {
        return time.ToString(@"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture);
    }

    // This examples shows how to use Whisper.net to create a transcription from an audio file with 16Khz sample rate.
    // It uses both Cuda (NVidia GPU) or CPU, and loads the first one that is available.
    public static async Task TranscribeAsync(string inputFileName)
    {
        // We declare three variables which we will use later, ggmlType, modelFileName and wavFileName
        var ggmlType = GgmlType.LargeV3Turbo;
        var modelFileName = "ggml-largev3-turbo.bin";
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
        using var processor = whisperFactory.CreateBuilder()
            .WithLanguage("auto")
            .Build();
            

        
        using var fileStream = File.OpenRead(wavFileName);
        using var writer = new StreamWriter($"{Path.GetDirectoryName(wavFileName)}\\{Path.GetFileNameWithoutExtension(wavFileName)}.srt");
        int index = 1;

        // This section processes the audio file and prints the results (start time, end time and text) to the console.
        await foreach (var result in processor.ProcessAsync(fileStream))
        {
            Console.WriteLine($"{result.Start}->{result.End}: {result.Text}");
            IdentifiedLanguage = result.Language;
            await writer.WriteLineAsync(index.ToString());
            await writer.WriteLineAsync($"{FormatSrtTime(result.Start)} --> {FormatSrtTime(result.End)}");
            await writer.WriteLineAsync(result.Text.Trim());
            await writer.WriteLineAsync();
            
            index++;
        }
    }

    private static async Task DownloadModel(string fileName, GgmlType ggmlType)
    {
        Console.WriteLine($"Downloading Model {fileName}");
        using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
        using var fileWriter = File.OpenWrite(fileName);
        await modelStream.CopyToAsync(fileWriter);
    }


}
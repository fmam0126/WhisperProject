
using System.Security;

namespace WhisperProject;

class Program
{
    static async Task Main(string[] args)
    {
        List<string> sourceFiles;
        
        Console.WriteLine("Input a path to a folder:");
        string? readResult = Console.ReadLine();
        if (readResult == null)
        {
            throw new Exception("Filepath cannot be null");
        }
        
        FileConvert fileConvert = new FileConvert(readResult);
        
        sourceFiles = FolderParser.FindSourceFiles(readResult);
        foreach (var item in sourceFiles)
        {
            string outputPath;
            Console.WriteLine(Path.GetExtension(item));
            if (Path.GetExtension(item) != ".wav")
            {
                outputPath = await fileConvert.ConvertToWav(item);
            }
            else
            {
                outputPath = item;
            }
            Console.WriteLine($"sending {Path.GetFileName(outputPath)} to Whisper");
            try
            {
                
            // string? transcription = await whisperClient.TranscribeAsync("whisper-v3", "DUMMY", outputPath);
            await WhisperClient.TranscribeAsync(outputPath);
            Console.WriteLine(Path.GetDirectoryName(outputPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine($"Finished processing {Path.GetFileName(outputPath)}");
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("Tranlating subtitles...");
            SubtitleTranslator subtitleTranslator = new SubtitleTranslator
            {
                Url = "http://127.0.0.1",
                Port = 1234,
                TargetLanguage = "en",
                ApiKey = "DUMMY",
                GptPath = "/v1/chat/completions",
                Model = "google/gemma-3-4b"
                
                
            };
            string srtFileName = $"{Path.GetDirectoryName(outputPath)}\\{Path.GetFileNameWithoutExtension(outputPath)}.srt";
            await subtitleTranslator.TranslateSrtAsync(srtFileName);
            Console.WriteLine($"Finished Translating {Path.GetFileName(item)}");
            // File.Delete(outputPath);

        }
        
    }
}

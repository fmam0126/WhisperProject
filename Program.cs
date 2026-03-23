using Microsoft.Extensions.Configuration;
using System.Security;

namespace WhisperProject;

class Program
{
    static async Task Main(string[] args)
    {
        // var input = args[0];
        // Console.WriteLine(input);
        // Console.WriteLine("Input a path to a folder:");
        // string? readResult = Console.ReadLine();
        // if (readResult == null)
        // {
        //     throw new Exception("Filepath cannot be null");
        // }

        // Build a Config object to read from appsettings.json and environment variables
        IConfigurationRoot config;
        try
        {
            config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("./appsettings.json", optional: false)
                .Build();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            return;
        }
        Settings? settings;
        try
        {
            settings = config.GetSection("Settings").Get<Settings>();
            if (settings == null)
            {
                throw new Exception("Settings section is missing in appsettings.json");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing settings: {ex.Message}");
            return;
        }




        List<string> sourceFiles;
        FileConvert fileConvert = new FileConvert(settings.InputPath);

        sourceFiles = FolderParser.FindSourceFiles(settings.InputPath);
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
                Url = settings.Url,
                Port = settings.Port,
                TargetLanguage = settings.TargetLanguage,
                ApiKey = settings.ApiKey,
                GptPath = settings.GptPath,
                Model = settings.GptModel
            };
            string srtFileName = $"{Path.GetDirectoryName(outputPath)}\\{Path.GetFileNameWithoutExtension(outputPath)}.srt";
            await subtitleTranslator.TranslateSrtAsync(srtFileName);
            Console.WriteLine($"Finished Translating {Path.GetFileName(outputPath)}");
            File.Delete(outputPath);

        }

    }
}

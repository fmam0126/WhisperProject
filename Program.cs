using Microsoft.Extensions.Configuration;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using WhisperProject.Class;
using WhisperProject.Models;

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
            // Apply voice emphasis filter if enabled in settings
            if (settings.ApplyVoiceEmphasisFilter)
            {
                Console.WriteLine("Applying voice emphasis filter...");
                VoiceEmphasisFilter filter = new VoiceEmphasisFilter();

                string filteredOutputPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty, $"{Path.GetFileNameWithoutExtension(outputPath)}.filtered.wav");

                filter.ApplyVoiceEmphasis(outputPath, filteredOutputPath);
                File.Copy(filteredOutputPath, outputPath, overwrite: true); // Replace original file with filtered file 
                File.Delete(filteredOutputPath); // Clean up the intermediate filtered file
            }

            // transcribe the file and save the srt in the same directory as the original file
            Console.WriteLine($"sending {Path.GetFileName(item)} to Whisper");
            try
            {

                // string? transcription = await whisperClient.TranscribeAsync("whisper-v3", "DUMMY", outputPath);
                await WhisperClient.TranscribeVadAsync(outputPath, modelPath: settings.WhisperModelpath, language: settings.WhisperLanguage);
                // await WhisperClient.TranscribeAsync(outputPath, language: settings.WhisperLanguage, modelPath: settings.WhisperModelpath, useVad: settings.UseVoiceActivityDetection);
                Console.WriteLine(Path.GetDirectoryName(outputPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                break;
            }

            Console.WriteLine($"Finished processing {Path.GetFileName(item)}");
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
            await subtitleTranslator.TranslateSrtAsync(srtFileName, Path.GetDirectoryName(item) ?? outputPath);
            Console.WriteLine($"Finished Translating {Path.GetFileName(item)}");

            // Clean up temporary files
            File.Delete(srtFileName); // Delete the temporary translated srt file
            File.Delete(outputPath);
            if (!Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(outputPath) ?? string.Empty).Any())
            {
                Directory.Delete(Path.GetDirectoryName(outputPath) ?? string.Empty); // Delete the temp directory if it's empty
            }

        }

    }
}

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

            outputPath = await fileConvert.ConvertToWav(item);

            // Apply voice emphasis filter if enabled in settings
            // redo with better filter
            if (settings.ApplyVoiceEmphasisFilter)
            {
                Console.WriteLine("Applying voice emphasis filter...");
                VoiceEmphasisFilter filter = new VoiceEmphasisFilter();

                string filteredOutputPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty, $"{Path.GetFileNameWithoutExtension(outputPath)}.filtered.wav");

                // filter.ApplyVoiceEmphasis(outputPath, filteredOutputPath);
                try
                {
                    filter.ApplyDpdfNetVoiceEnhancement(outputPath, filteredOutputPath, settings.DpdfNetModelPath);

                }
                catch (System.Exception ex)
                {
                    Console.WriteLine($"Error applying voice enhancement: {ex.Message}");
                    break;
                }
                File.Copy(filteredOutputPath, outputPath, overwrite: true); // Replace original file with filtered file 
                File.Delete(filteredOutputPath); // Clean up the intermediate filtered file
            }

            // transcribe the file and save the srt in the same directory as the original file
            Console.WriteLine($"sending {Path.GetFileName(item)} to Whisper");
            try
            {
                if (settings.UseVoiceActivityDetection)
                {
                    await WhisperClient.TranscribeVadAsync(outputPath, modelPath: settings.WhisperModelpath, language: settings.WhisperLanguage);
                }
                else
                {
                    await WhisperClient.TranscribeAsync(outputPath, language: settings.WhisperLanguage, modelPath: settings.WhisperModelpath);
                }
                Console.WriteLine(Path.GetDirectoryName(outputPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {Path.GetFileName(item)}: {ex}");
                continue;
            }

            Console.WriteLine($"Finished processing {Path.GetFileName(item)}");
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("Tranlating subtitles...");
            try
            {
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Translation error for {Path.GetFileName(item)}: {ex}");
            }

            // Clean up temporary files
            try
            {
                string srtFileName = $"{Path.GetDirectoryName(outputPath)}\\{Path.GetFileNameWithoutExtension(outputPath)}.srt";
                if (File.Exists(srtFileName)) File.Delete(srtFileName);
                if (File.Exists(outputPath)) File.Delete(outputPath);
                if (!Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(outputPath) ?? string.Empty).Any())
                {
                    Directory.Delete(Path.GetDirectoryName(outputPath) ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup error for {Path.GetFileName(item)}: {ex.Message}");
            }

        }

    }
}

using Microsoft.Extensions.Configuration;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using WhisperProject.Class;
using WhisperProject.Core;
using WhisperProject.Models;

namespace WhisperProject;

class Program
{
    static async Task Main(string[] args)
    {
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

        SubtitleTranslator subtitleTranslator = new SubtitleTranslator
        {
            Url = settings.Url,
            Port = settings.Port,
            TargetLanguage = settings.TargetLanguage,
            ApiKey = settings.ApiKey,
            GptPath = settings.GptPath,
            Model = settings.GptModel,
            Concurrency = settings.Concurrency,
            ContextSize = settings.ContextSize,
            SystemPrompt = settings.SystemPrompt
        };

        string inputPath = Path.EndsInDirectorySeparator(settings.InputPath) ? settings.InputPath : settings.InputPath + Path.DirectorySeparatorChar;
        List<string> sourceFiles;
        FileConvert fileConvert = new FileConvert(inputPath);

        sourceFiles = FolderParser.FindSourceFiles(inputPath);
        foreach (var item in sourceFiles)
        {
            string outputPath;

            Console.WriteLine(Path.GetExtension(item));

            outputPath = await fileConvert.ConvertToWav(item);

            // Apply voice emphasis filter if enabled in settings
            // redo with better filter
            if (settings.ApplyDpdfNet)
            {
                Console.WriteLine("Applying DpdfNet voice enhancement...");
                VoiceEmphasisFilter filter = new VoiceEmphasisFilter();

                string filteredOutputPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? string.Empty, $"{Path.GetFileNameWithoutExtension(outputPath)}.filtered.wav");

                // filter.ApplyVoiceEmphasis(outputPath, filteredOutputPath);
                try
                {
                    await filter.ApplyDpdfNetVoiceEnhancement(outputPath, filteredOutputPath, settings.DpdfNetModelPath, settings.DpdfNetDownloadUrl);

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
            //try
            //{

            //    await Qwen3Asr.RunQwen3Asr(outputPath, Directory.GetCurrentDirectory());
            //}
            //catch (System.Exception ex)
            //{
            //    Console.WriteLine($"Error transcribing {Path.GetFileName(item)}: {ex.Message}");
            //    continue;
            //}
            try
            {
                string? identifiedLanguage;
                if (settings.UseVoiceActivityDetection)
                {
                    identifiedLanguage = await WhisperClient.TranscribeVadAsync(outputPath, modelPath: settings.WhisperModelPath, language: settings.WhisperLanguage);
                }
                else
                {
                    identifiedLanguage = await WhisperClient.TranscribeAsync(outputPath, language: settings.WhisperLanguage, modelPath: settings.WhisperModelPath);
                }
                subtitleTranslator.SourceLanguage = identifiedLanguage ?? string.Empty;
                Console.WriteLine(Path.GetDirectoryName(outputPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {Path.GetFileName(item)}: {ex}");
                continue;
            }

            Console.WriteLine($"Finished processing {Path.GetFileName(item)}");
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("Translating subtitles...");
            try
            {
                string srtFileName = $"{Path.GetDirectoryName(outputPath)}\\{Path.GetFileNameWithoutExtension(outputPath)}.srt";
                switch (settings.UseContextTranslation)
                {
                    case true:
                        await subtitleTranslator.TranslateSrtWithContextAsync(srtFileName, Path.GetDirectoryName(item) ?? outputPath);
                        break;
                    case false:
                        await subtitleTranslator.TranslateSrtAsync(srtFileName, Path.GetDirectoryName(item) ?? outputPath);
                        break;
                }
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
                // if the directory is empty after deleting the files, delete the directory as well
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

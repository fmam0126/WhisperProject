using System.IO;
using FFMpegCore;
using FFMpegCore.Enums;

namespace WhisperProject.Class;

public class FileConvert
{
    // Define the working directory
    public string WorkingPath = string.Empty;
    public FileConvert(string workingPath)
    {
        WorkingPath = workingPath + "temp";
    }
    /// <summary>
    /// Converts the input audio/video file to a 16Khz mono WAV file.
    /// </summary>
    /// <param name="inputPath">The path to the input audio/video file</param>
    /// <returns>The path to the converted WAV file</returns>
    public async Task<string> ConvertToWav(string inputPath)
    {
        // 1. Ensure the directory exists
        if (!Directory.Exists(WorkingPath))
        {
            Directory.CreateDirectory(WorkingPath);
        }

        // 2. Get the filename without the extension (e.g., "video" from "video.mp4")
        string fileNameOnly = Path.GetFileNameWithoutExtension(inputPath);

        // 3. Combine to create the full output path (e.g., ".../temp_audio/video.wav")
        string outputPath = Path.Combine(WorkingPath, $"{fileNameOnly}.wav");
        if (File.Exists(outputPath))
        {
            return outputPath;
        }
        // 4. Perform the extraction
        await FFMpegArguments
        .FromFileInput(inputPath)
        .OutputToFile(outputPath, overwrite: true, options => options
            .WithAudioCodec("pcm_s16le")
            .WithCustomArgument("-ac 1") // Mono channel
            .WithAudioSamplingRate(16000)
            .ForceFormat("wav"))
            .ProcessAsynchronously();
        return outputPath;
    }
}
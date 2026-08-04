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

    // TODO: Decide if the method should take an output path as an input
    /// <summary>
    /// converts file to mp3 using the FFMpeg.ExttractAudio and saves it in a temp directory next to the original file
    /// </summary>
    /// <param name="inputPath">file to be converted</param>
    /// <exception cref="Exception">throw exception if output file already exists</exception>
    public async Task<string> ConvertToMp3(string inputPath)
    {
        // 1. Ensure the directory exists
        if (!Directory.Exists(WorkingPath))
        {
            Directory.CreateDirectory(WorkingPath);
        }

        // 2. Get the filename without the extension (e.g., "video" from "video.mp4")
        string fileNameOnly = Path.GetFileNameWithoutExtension(inputPath);

        // 3. Combine to create the full output path (e.g., ".../temp_audio/video.mp3")
        string outputPath = Path.Combine(WorkingPath, $"{fileNameOnly}.mp3");
        if (File.Exists(outputPath))
        {
            return outputPath;
            // throw new Exception("Output file already Exists");
        }
        // 4. Perform the extraction
        FFMpeg.ExtractAudio(inputPath, outputPath);
        return outputPath;
    }
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
            // throw new Exception("Output file already Exists");
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
using System.IO;
using FFMpegCore;

class FileConvert
{
    // Define the working directory
    public string WorkingPath = string.Empty;
    public FileConvert(string workingPath)
    {
        WorkingPath = workingPath +"\\temp";
    }

    public void ConvertToMp3(string inputPath)
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
            throw new Exception("Output file already Exists");
        }
        // 4. Perform the extraction
        FFMpeg.ExtractAudio(inputPath, outputPath);
    }
}
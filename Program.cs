
using System.Security;

namespace WhisperProject;

class Program
{
    static async Task Main(string[] args)
    {
        List<string> sourceFiles;
        WhisperClient whisperClient = new WhisperClient();
        
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
            
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            // File.Delete(outputPath);

        }
        
    }
}

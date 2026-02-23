
namespace WhisperProject;

class Program
{
    static void Main(string[] args)
    {
        List<string> files;
        
        Console.WriteLine("Input a path to a folder:");
        string? readResult = Console.ReadLine();
        if (readResult == null)
        {
            throw new Exception("Filepath cannot be null");
        }
        FileConvert fileConvert = new FileConvert(readResult);
        
        files = FolderParser.FindSourceFiles(readResult);
        foreach (var item in files)
        {
            if (Path.GetExtension(item) != "mp3")
            {
                fileConvert.ConvertToMp3(item);
            }
        }
        
    }
}


namespace WhisperProject;

class Program
{
    static void Main(string[] args)
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
            Console.WriteLine(Path.GetExtension(item));
            if (Path.GetExtension(item) != ".mp3")
            {
                //fileConvert.ConvertToMp3(item);
            }
        }
        
    }
}

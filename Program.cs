namespace WhisperProject;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Input a path to a folder:");
        string? readResult = Console.ReadLine();

        if (readResult != null)
        {
            Console.WriteLine(FolderParser.FindSourceFiles(readResult));
        }
        
        
    }
}

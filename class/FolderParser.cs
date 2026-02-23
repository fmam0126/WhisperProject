
class FolderParser
{
    /// <summary>
    /// finds all mp4, mkv and mp3 files in the input folder and its subdirectories
    /// </summary>
    /// <param name="folderPath">input path to folder that shoud be parsed</param>
    /// <returns>returns path to mp4, mkv and mp3 files</returns>
    public static string FindSourceFiles(string folderPath)
    {
        var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(s => s.ToLower().EndsWith(".mp4") || s.ToLower().EndsWith(".mkv") || s.ToLower().EndsWith(".mp3"));

        return string.Join("\n", files);
    

    }
}

class FolderParser
{
    /// <summary>
    /// finds all mp4, mkv and mp3 files in the input folder and its subdirectories
    /// </summary>
    /// <param name="folderPath">input path to folder that shoud be parsed</param>
    /// <returns>returns path to mp4, mkv and mp3 files</returns>
    public static List<string> FindSourceFiles(string folderPath)
    {
        var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(s => s.ToLower().EndsWith(".mp4") || s.ToLower().EndsWith(".mkv") || s.ToLower().EndsWith(".mp3"));

        return files.ToList();
    }
    /// <summary>
    /// finds all exising subtitles (.srt) in a specified folder path
    /// </summary>
    /// <param name="folderPath">input folder path to be parsed</param>
    /// <returns>returns a list of paths to .srt files</returns>
    public static List<string> FindExistingSubtitles(string folderPath)
    {
        var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(s => s.ToLower().EndsWith(".srt"));

        return files.ToList();
    }
}
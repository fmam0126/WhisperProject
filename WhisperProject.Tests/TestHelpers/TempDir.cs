namespace WhisperProject.Tests.TestHelpers;

/// <summary>
/// Creates a unique temporary directory that is deleted on disposal.
/// Each instance gets a unique Guid-suffixed subdirectory so xUnit
/// parallelisation is safe.
/// </summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; }

    public TempDir()
    {
        Path = System.IO.Directory.CreateTempSubdirectory("wp-tests-").FullName;
    }

    /// <summary>
    /// Creates a file at <paramref name="relativePath"/> containing
    /// <paramref name="content"/> text. Parent directories are created automatically.
    /// Returns the full file path.
    /// </summary>
    public string CreateFile(string relativePath, string content = "")
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        System.IO.File.WriteAllText(fullPath, content);
        return fullPath;
    }

    /// <summary>
    /// Creates an empty file at <paramref name="relativePath"/>.
    /// Returns the full file path.
    /// </summary>
    public string TouchFile(string relativePath)
    {
        return CreateFile(relativePath, string.Empty);
    }

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}

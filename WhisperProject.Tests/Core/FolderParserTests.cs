using WhisperProject.Class;
using WhisperProject.Tests.TestHelpers;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for <see cref="FolderParser.FindSourceFiles"/> and
/// <see cref="FolderParser.FindExistingSubtitles"/>.
/// All tests use unique temp directories to allow xUnit parallelisation.
/// </summary>
public class FolderParserTests
{
    [Fact]
    public void FindSourceFiles_MixedExtensions_ReturnsOnlyMediaFiles()
    {
        using var dir = new TempDir();
        dir.TouchFile("a.mp4");
        dir.TouchFile("b.mkv");
        dir.TouchFile("c.mp3");
        dir.TouchFile("d.srt");
        dir.TouchFile("e.txt");
        dir.TouchFile("f.wav");

        var result = FolderParser.FindSourceFiles(dir.Path);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, p => p.EndsWith("a.mp4"));
        Assert.Contains(result, p => p.EndsWith("b.mkv"));
        Assert.Contains(result, p => p.EndsWith("c.mp3"));
    }

    [Fact]
    public void FindSourceFiles_UppercaseExtensions_Included()
    {
        using var dir = new TempDir();
        dir.TouchFile("A.MP4");
        dir.TouchFile("B.MKV");
        dir.TouchFile("C.MP3");

        var result = FolderParser.FindSourceFiles(dir.Path);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void FindSourceFiles_NestedSubdirectories_Recurses()
    {
        using var dir = new TempDir();
        var sub = System.IO.Path.Combine(dir.Path, "sub", "deep");
        System.IO.Directory.CreateDirectory(sub);
        System.IO.File.WriteAllText(System.IO.Path.Combine(sub, "nested.mp4"), "");

        var result = FolderParser.FindSourceFiles(dir.Path);

        Assert.Single(result);
        Assert.EndsWith("nested.mp4", result[0]);
    }

    [Fact]
    public void FindSourceFiles_EmptyDirectory_ReturnsEmptyList()
    {
        using var dir = new TempDir();

        var result = FolderParser.FindSourceFiles(dir.Path);

        Assert.Empty(result);
    }

    [Fact]
    public void FindSourceFiles_MissingDirectory_ThrowsDirectoryNotFoundException()
    {
        var missingPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"nonexistent-{Guid.NewGuid()}");

        Assert.Throws<System.IO.DirectoryNotFoundException>(
            () => FolderParser.FindSourceFiles(missingPath));
    }

    [Fact]
    public void FindExistingSubtitles_MixedFiles_ReturnsOnlySrt()
    {
        using var dir = new TempDir();
        dir.TouchFile("a.srt");
        dir.TouchFile("b.SRT");
        dir.TouchFile("c.mp4");
        dir.TouchFile("d.txt");

        var result = FolderParser.FindExistingSubtitles(dir.Path);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FindExistingSubtitles_EmptyDirectory_ReturnsEmptyList()
    {
        using var dir = new TempDir();

        var result = FolderParser.FindExistingSubtitles(dir.Path);

        Assert.Empty(result);
    }

    [Fact]
    public void FindExistingSubtitles_NestedSubdirectories_Recurses()
    {
        using var dir = new TempDir();
        var sub = System.IO.Path.Combine(dir.Path, "sub");
        System.IO.Directory.CreateDirectory(sub);
        System.IO.File.WriteAllText(System.IO.Path.Combine(sub, "subtitle.srt"), "");

        var result = FolderParser.FindExistingSubtitles(dir.Path);

        Assert.Single(result);
        Assert.EndsWith("subtitle.srt", result[0]);
    }
}

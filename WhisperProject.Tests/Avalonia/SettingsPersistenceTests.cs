using WhisperProject.Avalonia;
using WhisperProject.Avalonia.ViewModels;
using WhisperProject.Tests.TestHelpers;

namespace WhisperProject.Tests.Avalonia;

/// <summary>
/// Tests for <see cref="MainWindow.LoadSettingsOrDefaults"/> and
/// <see cref="MainWindow.SaveSettingsToFile"/> — the settings persistence
/// layer that reads and writes <c>appsettings.json</c>.
/// </summary>
public class SettingsPersistenceTests
{
    // ── Save + Load round-trip ───────────────────────────────────────────

    [Fact]
    public void SaveThenLoadRoundTripPreservesAllValues()
    {
        using var dir = new TempDir();

        var original = new OptionsViewModel
        {
            Url = "http://192.168.1.50",
            Port = "8080",
            ApiKey = "secret-key",
            GptPath = "/api/custom",
            GptModel = "llama3.1-8b",
            TargetLanguage = "Norwegian",
            WhisperModelPath = @"C:\models\ggml-largev2.bin",
            WhisperLanguage = "no",
            Concurrency = "8",
            ContextSize = "20",
            SystemPrompt = "Custom system prompt for testing.",
            DpdfNetModelPath = @"C:\models\dpdfnet8.onnx",
            DpdfNetDownloadUrl = "https://example.com/model.onnx",
        };
        original.UsePerLineTranslation = true;     // → UseContextTranslation = false
        original.VadDisabled = true;               // → UseVoiceActivityDetection = false
        original.DpdfNetEnabled = true;            // → ApplyDpdfNet = true

        MainWindow.SaveSettingsToFile(original, dir.Path);
        var loaded = MainWindow.LoadSettingsOrDefaults(dir.Path);

        Assert.Equal(original.Url, loaded.Url);
        Assert.Equal(original.Port, loaded.Port);
        Assert.Equal(original.ApiKey, loaded.ApiKey);
        Assert.Equal(original.GptPath, loaded.GptPath);
        Assert.Equal(original.GptModel, loaded.GptModel);
        Assert.Equal(original.TargetLanguage, loaded.TargetLanguage);
        Assert.Equal(original.WhisperModelPath, loaded.WhisperModelPath);
        Assert.Equal(original.WhisperLanguage, loaded.WhisperLanguage);
        Assert.Equal(original.Concurrency, loaded.Concurrency);
        Assert.Equal(original.ContextSize, loaded.ContextSize);
        Assert.Equal(original.SystemPrompt, loaded.SystemPrompt);
        Assert.Equal(original.DpdfNetModelPath, loaded.DpdfNetModelPath);
        Assert.Equal(original.DpdfNetDownloadUrl, loaded.DpdfNetDownloadUrl);

        // Radio groups
        Assert.False(loaded.UseContextTranslation);
        Assert.True(loaded.UsePerLineTranslation);
        Assert.False(loaded.VadEnabled);
        Assert.True(loaded.VadDisabled);
        Assert.True(loaded.DpdfNetEnabled);
        Assert.False(loaded.DpdfNetDisabled);
    }

    [Fact]
    public void SaveThenLoadPreservesDefaultValues()
    {
        using var dir = new TempDir();

        var original = new OptionsViewModel(); // all defaults
        MainWindow.SaveSettingsToFile(original, dir.Path);
        var loaded = MainWindow.LoadSettingsOrDefaults(dir.Path);

        Assert.Equal(original.Url, loaded.Url);
        Assert.Equal(original.Port, loaded.Port);
        Assert.Equal(original.GptPath, loaded.GptPath);
        Assert.Equal(original.GptModel, loaded.GptModel);
        Assert.Equal(original.TargetLanguage, loaded.TargetLanguage);
        Assert.Equal(original.Concurrency, loaded.Concurrency);
        Assert.Equal(original.ContextSize, loaded.ContextSize);
        Assert.True(loaded.UseContextTranslation);
        Assert.False(loaded.UsePerLineTranslation);
        Assert.True(loaded.VadEnabled);
        Assert.False(loaded.VadDisabled);
        Assert.False(loaded.DpdfNetEnabled);
        Assert.True(loaded.DpdfNetDisabled);
    }

    [Fact]
    public void SaveCreatesJsonFileOnDisk()
    {
        using var dir = new TempDir();
        var filePath = System.IO.Path.Combine(dir.Path, "appsettings.json");

        Assert.False(System.IO.File.Exists(filePath));

        MainWindow.SaveSettingsToFile(new OptionsViewModel(), dir.Path);

        Assert.True(System.IO.File.Exists(filePath));
        var content = System.IO.File.ReadAllText(filePath);
        Assert.Contains("\"Settings\"", content);
        Assert.Contains("\"Url\"", content);
        Assert.Contains("\"Port\"", content);
    }

    [Fact]
    public void SaveOverwritesExistingFile()
    {
        using var dir = new TempDir();
        var filePath = System.IO.Path.Combine(dir.Path, "appsettings.json");

        // Write an initial file with known content
        System.IO.File.WriteAllText(filePath, "old content");

        var vm = new OptionsViewModel { Url = "http://new.example.com" };
        MainWindow.SaveSettingsToFile(vm, dir.Path);

        var content = System.IO.File.ReadAllText(filePath);
        Assert.DoesNotContain("old content", content);
        Assert.Contains("http://new.example.com", content);
    }

    // ── Load fallbacks ──────────────────────────────────────────────────

    [Fact]
    public void LoadReturnsDefaultsWhenFileDoesNotExist()
    {
        using var dir = new TempDir();
        // Don't create any file

        var loaded = MainWindow.LoadSettingsOrDefaults(dir.Path);

        // Should return a ViewModel with default values
        Assert.Equal("http://127.0.0.1", loaded.Url);
        Assert.Equal("1234", loaded.Port);
        Assert.True(loaded.UseContextTranslation);
        Assert.True(loaded.VadEnabled);
    }

    [Fact]
    public void LoadReturnsDefaultsWhenJsonIsMalformed()
    {
        using var dir = new TempDir();
        dir.CreateFile("appsettings.json", "this is not valid json {{{");

        var loaded = MainWindow.LoadSettingsOrDefaults(dir.Path);

        // Should not throw; should return defaults
        Assert.Equal("http://127.0.0.1", loaded.Url);
        Assert.Equal("1234", loaded.Port);
    }

    [Fact]
    public void LoadReturnsDefaultsWhenSettingsSectionIsMissing()
    {
        using var dir = new TempDir();
        dir.CreateFile("appsettings.json", """
        {
            "OtherSection": {
                "Key": "Value"
            }
        }
        """);

        var loaded = MainWindow.LoadSettingsOrDefaults(dir.Path);

        // Should not throw; should return defaults
        Assert.Equal("http://127.0.0.1", loaded.Url);
        Assert.Equal("1234", loaded.Port);
    }

    [Fact]
    public void LoadReturnsDefaultsWhenSettingsSectionIsEmpty()
    {
        using var dir = new TempDir();
        dir.CreateFile("appsettings.json", """
        {
            "Settings": {}
        }
        """);

        var loaded = MainWindow.LoadSettingsOrDefaults(dir.Path);

        // Empty Settings object → default values for everything
        Assert.Equal("http://127.0.0.1", loaded.Url);
        Assert.True(loaded.UseContextTranslation);
    }

    [Fact]
    public void LoadReadsPartialSettingsAndFallsBackForMissingKeys()
    {
        using var dir = new TempDir();
        dir.CreateFile("appsettings.json", """
        {
            "Settings": {
                "Url": "http://partial.example.com",
                "Port": 9999,
                "TargetLanguage": "French"
            }
        }
        """);

        var loaded = MainWindow.LoadSettingsOrDefaults(dir.Path);

        // Provided keys should be read
        Assert.Equal("http://partial.example.com", loaded.Url);
        Assert.Equal("9999", loaded.Port);
        Assert.Equal("French", loaded.TargetLanguage);

        // Missing keys fall back to Settings model defaults (Concurrency = 4)
        Assert.Equal("4", loaded.Concurrency);
        Assert.Equal("10", loaded.ContextSize);
    }

    [Fact]
    public void LoadWithNullDirectoryFallsBackToCurrentDirectory()
    {
        // Passing null should fall back to Directory.GetCurrentDirectory()
        // without throwing. If there happens to be an appsettings.json in CWD
        // it will be loaded; otherwise defaults are returned. Either outcome
        // is fine — we just verify it doesn't throw.
        var loaded = MainWindow.LoadSettingsOrDefaults(configDirectory: null);

        Assert.NotNull(loaded);
        Assert.False(string.IsNullOrEmpty(loaded.Url));
    }

    [Fact]
    public void SaveWithNullDirectoryWritesToCurrentDirectory()
    {
        // Should not throw when directory is null (falls back to CWD).
        // We don't assert file content since CWD is shared, but it must
        // not throw.
        var vm = new OptionsViewModel();
        var ex = Record.Exception(() =>
            MainWindow.SaveSettingsToFile(vm, configDirectory: null));

        Assert.Null(ex);
    }
}

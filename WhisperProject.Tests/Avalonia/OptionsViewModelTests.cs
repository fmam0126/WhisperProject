using WhisperProject.Avalonia.ViewModels;
using WhisperProject.Models;
using WhisperProject.Tests.TestHelpers;

namespace WhisperProject.Tests.Avalonia;

/// <summary>
/// Tests for <see cref="OptionsViewModel"/>: defaults, radio-group mutual
/// exclusion, <c>ToSettings</c> parse fallbacks, and settings round-trip.
/// </summary>
public class OptionsViewModelTests
{
    // ── Defaults ──────────────────────────────────────────────────────────

    [Fact]
    public void Defaults_AllProperties_HaveExpectedValues()
    {
        var vm = new OptionsViewModel();

        Assert.Equal("http://127.0.0.1", vm.Url);
        Assert.Equal("1234", vm.Port);
        Assert.Equal("/v1", vm.GptPath);
        Assert.Equal("google/gemma-4-e2b", vm.GptModel);
        Assert.Equal("English", vm.TargetLanguage);
        Assert.Equal("", vm.ApiKey);
        Assert.Equal("", vm.WhisperModelPath);
        Assert.Equal("", vm.WhisperLanguage);
        Assert.Equal("4", vm.Concurrency);
        Assert.Equal("10", vm.ContextSize);
        Assert.NotEmpty(vm.SystemPrompt);
        Assert.Equal("", vm.DpdfNetModelPath);
        Assert.NotEmpty(vm.DpdfNetDownloadUrl);
        // Radio defaults
        Assert.True(vm.UseContextTranslation);
        Assert.False(vm.UsePerLineTranslation);
        Assert.True(vm.VadEnabled);
        Assert.False(vm.VadDisabled);
        Assert.False(vm.DpdfNetEnabled);
        Assert.True(vm.DpdfNetDisabled);
    }

    // ── Radio group mutual exclusion ──────────────────────────────────────

    [Fact]
    public void UsePerLineTranslation_True_UnsetsUseContextTranslation()
    {
        var vm = new OptionsViewModel();
        Assert.True(vm.UseContextTranslation);

        vm.UsePerLineTranslation = true;

        Assert.True(vm.UsePerLineTranslation);
        Assert.False(vm.UseContextTranslation);
    }

    [Fact]
    public void UseContextTranslation_True_UnsetsUsePerLineTranslation()
    {
        var vm = new OptionsViewModel();
        vm.UsePerLineTranslation = true; // flip first
        Assert.True(vm.UsePerLineTranslation);

        vm.UseContextTranslation = true;

        Assert.True(vm.UseContextTranslation);
        Assert.False(vm.UsePerLineTranslation);
    }

    [Fact]
    public void SetAlreadyTrue_DoesNotTouchSibling()
    {
        var vm = new OptionsViewModel();
        // Both should stay: context=true, perLine=false

        vm.UseContextTranslation = true; // same value — SetProperty short-circuits

        Assert.True(vm.UseContextTranslation);
        Assert.False(vm.UsePerLineTranslation);
    }

    [Fact]
    public void UsePerLineTranslation_False_DoesNotReenableContext()
    {
        var vm = new OptionsViewModel();
        vm.UsePerLineTranslation = true; // perLine on, context off
        Assert.True(vm.UsePerLineTranslation);
        Assert.False(vm.UseContextTranslation);

        vm.UsePerLineTranslation = false; // turning off — no auto-reeanble

        Assert.False(vm.UsePerLineTranslation);
        Assert.False(vm.UseContextTranslation); // stays false
    }

    // VAD group
    [Fact]
    public void VadDisabled_True_UnsetsVadEnabled()
    {
        var vm = new OptionsViewModel();
        vm.VadDisabled = true;
        Assert.True(vm.VadDisabled);
        Assert.False(vm.VadEnabled);
    }

    [Fact]
    public void VadEnabled_True_UnsetsVadDisabled()
    {
        var vm = new OptionsViewModel();
        vm.VadDisabled = true;
        vm.VadEnabled = true;
        Assert.True(vm.VadEnabled);
        Assert.False(vm.VadDisabled);
    }

    // DpdfNet group
    [Fact]
    public void DpdfNetEnabled_True_UnsetsDpdfNetDisabled()
    {
        var vm = new OptionsViewModel();
        vm.DpdfNetEnabled = true;
        Assert.True(vm.DpdfNetEnabled);
        Assert.False(vm.DpdfNetDisabled);
    }

    [Fact]
    public void DpdfNetDisabled_True_UnsetsDpdfNetEnabled()
    {
        var vm = new OptionsViewModel();
        vm.DpdfNetEnabled = true;
        vm.DpdfNetDisabled = true;
        Assert.True(vm.DpdfNetDisabled);
        Assert.False(vm.DpdfNetEnabled);
    }

    // ── ToSettings parse fallbacks ────────────────────────────────────────

    [Theory]
    [InlineData("1234", 1234)]
    [InlineData("65535", 65535)]
    [InlineData("0", 1234)]
    [InlineData("-1", 1234)]
    [InlineData("65536", 1234)]
    [InlineData("abc", 1234)]
    [InlineData("", 1234)]
    public void ToSettings_Port_ValidatesAndFallsBack(string input, int expected)
    {
        var vm = new OptionsViewModel { Port = input };

        var settings = vm.ToSettings(@"C:\test");

        Assert.Equal(expected, settings.Port);
    }

    [Theory]
    [InlineData("4", 4u)]
    [InlineData("0", 4u)]
    [InlineData("-1", 4u)]
    [InlineData("abc", 4u)]
    [InlineData("16", 16u)]
    public void ToSettings_Concurrency_ValidatesAndFallsBack(string input, uint expected)
    {
        var vm = new OptionsViewModel { Concurrency = input };

        var settings = vm.ToSettings(@"C:\test");

        Assert.Equal(expected, settings.Concurrency);
    }

    [Theory]
    [InlineData("10", 10u)]
    [InlineData("0", 10u)]
    [InlineData("3", 3u)]
    [InlineData("abc", 10u)]
    public void ToSettings_ContextSize_ValidatesAndFallsBack(string input, uint expected)
    {
        var vm = new OptionsViewModel { ContextSize = input };

        var settings = vm.ToSettings(@"C:\test");

        Assert.Equal(expected, settings.ContextSize);
    }

    [Fact]
    public void ToSettings_MapsAllFields_AndSetsInputPath()
    {
        var vm = new OptionsViewModel
        {
            Url = "http://example.com",
            Port = "8080",
            ApiKey = "key123",
            GptPath = "/api",
            GptModel = "gpt-4",
            TargetLanguage = "Norwegian",
            WhisperModelPath = "model.bin",
            WhisperLanguage = "no",
            Concurrency = "8",
            ContextSize = "20",
            SystemPrompt = "Custom system prompt",
            DpdfNetModelPath = "dpdf.onnx",
            DpdfNetDownloadUrl = "https://example.com/dpdf.onnx",
        };

        var settings = vm.ToSettings(@"C:\input");

        Assert.Equal(@"C:\input", settings.InputPath);
        Assert.Equal("http://example.com", settings.Url);
        Assert.Equal(8080, settings.Port);
        Assert.Equal("key123", settings.ApiKey);
        Assert.Equal("/api", settings.GptPath);
        Assert.Equal("gpt-4", settings.GptModel);
        Assert.Equal("Norwegian", settings.TargetLanguage);
        Assert.Equal("model.bin", settings.WhisperModelpath);
        Assert.Equal("no", settings.WhisperLanguage);
        Assert.Equal(8u, settings.Concurrency);
        Assert.Equal(20u, settings.ContextSize);
        Assert.Equal("Custom system prompt", settings.SystemPrompt);
        Assert.Equal("dpdf.onnx", settings.DpdfNetModelPath);
        Assert.Equal("https://example.com/dpdf.onnx", settings.DpdfNetDownloadUrl);
    }

    [Fact]
    public void ToSettings_RadioGroups_MapToFlags()
    {
        var vm = new OptionsViewModel();
        vm.UsePerLineTranslation = true;  // → UseContextTranslation=false
        vm.VadDisabled = true;            // → UseVoiceActivityDetection=false
        vm.DpdfNetEnabled = true;         // → ApplyDpdfNet=true

        var settings = vm.ToSettings(@"C:\test");

        Assert.False(settings.UseContextTranslation);
        Assert.False(settings.UseVoiceActivityDetection);
        Assert.True(settings.ApplyDpdfNet);
    }

    [Fact]
    public void LoadFromSettings_PopulatesAllProperties()
    {
        var settings = SettingsFactory.Create();
        settings.UseContextTranslation = false;
        settings.UseVoiceActivityDetection = false;
        settings.ApplyDpdfNet = true;

        var vm = new OptionsViewModel();
        vm.LoadFromSettings(settings);

        Assert.Equal(settings.Url, vm.Url);
        Assert.Equal(settings.Port.ToString(), vm.Port);
        Assert.Equal(settings.ApiKey, vm.ApiKey);
        Assert.Equal(settings.GptPath, vm.GptPath);
        Assert.Equal(settings.GptModel, vm.GptModel);
        Assert.Equal(settings.TargetLanguage, vm.TargetLanguage);
        Assert.Equal(settings.WhisperModelpath, vm.WhisperModelPath);
        // WhisperLanguage: Settings has null!, LoadFromSettings uses ?? string.Empty
        Assert.Equal(string.Empty, vm.WhisperLanguage);
        Assert.Equal(settings.Concurrency.ToString(), vm.Concurrency);
        Assert.Equal(settings.ContextSize.ToString(), vm.ContextSize);
        Assert.Equal(settings.SystemPrompt, vm.SystemPrompt);
        Assert.Equal(settings.DpdfNetModelPath, vm.DpdfNetModelPath);
        Assert.Equal(settings.DpdfNetDownloadUrl, vm.DpdfNetDownloadUrl);
        // Radio mappings
        Assert.False(vm.UseContextTranslation);
        Assert.True(vm.UsePerLineTranslation);
        Assert.False(vm.VadEnabled);
        Assert.True(vm.VadDisabled);
        Assert.True(vm.DpdfNetEnabled);
        Assert.False(vm.DpdfNetDisabled);
    }

    [Fact]
    public void LoadFromSettings_ToSettings_RoundTrip()
    {
        var original = SettingsFactory.Create();
        original.UseContextTranslation = false;
        original.UseVoiceActivityDetection = true;
        original.ApplyDpdfNet = false;
        original.Concurrency = 6;
        original.ContextSize = 15;

        var vm = new OptionsViewModel();
        vm.LoadFromSettings(original);

        var roundTripped = vm.ToSettings(original.InputPath);

        // All fields except null-forgiving WhisperLanguage default
        Assert.Equal(original.InputPath, roundTripped.InputPath);
        Assert.Equal(original.Url, roundTripped.Url);
        Assert.Equal(original.Port, roundTripped.Port);
        Assert.Equal(original.ApiKey, roundTripped.ApiKey);
        Assert.Equal(original.GptPath, roundTripped.GptPath);
        Assert.Equal(original.GptModel, roundTripped.GptModel);
        Assert.Equal(original.TargetLanguage, roundTripped.TargetLanguage);
        Assert.Equal(original.WhisperModelpath, roundTripped.WhisperModelpath);
        Assert.Equal(original.Concurrency, roundTripped.Concurrency);
        Assert.Equal(original.ContextSize, roundTripped.ContextSize);
        Assert.Equal(original.SystemPrompt, roundTripped.SystemPrompt);
        Assert.Equal(original.UseContextTranslation, roundTripped.UseContextTranslation);
        Assert.Equal(original.UseVoiceActivityDetection, roundTripped.UseVoiceActivityDetection);
        Assert.Equal(original.ApplyDpdfNet, roundTripped.ApplyDpdfNet);
        Assert.Equal(original.DpdfNetModelPath, roundTripped.DpdfNetModelPath);
        Assert.Equal(original.DpdfNetDownloadUrl, roundTripped.DpdfNetDownloadUrl);
    }
}

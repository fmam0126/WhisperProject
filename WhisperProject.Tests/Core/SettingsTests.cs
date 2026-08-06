using WhisperProject.Models;
using WhisperProject.Tests.TestHelpers;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for the <see cref="Settings"/> POCO default values and mutability.
/// </summary>
public class SettingsTests
{
    [Fact]
    public void DefaultsConcurrencyContextAndFlagsHaveExpectedValues()
    {
        var settings = SettingsFactory.Create();

        Assert.Equal(4u, settings.Concurrency);
        Assert.True(settings.UseContextTranslation);
        Assert.Equal(10u, settings.ContextSize);
        Assert.False(settings.ApplyDpdfNet);
        Assert.False(settings.UseVoiceActivityDetection);
        Assert.Equal(
            "https://github.com/k2-fsa/sherpa-onnx/releases/download/speech-enhancement-models/dpdfnet8.onnx",
            settings.DpdfNetDownloadUrl);
        Assert.NotEmpty(settings.SystemPrompt);
    }

    [Fact]
    public void DefaultsAreMutable()
    {
        var settings = SettingsFactory.Create();

        settings.Concurrency = 8;
        settings.UseContextTranslation = false;
        settings.ContextSize = 20;
        settings.ApplyDpdfNet = true;
        settings.UseVoiceActivityDetection = true;
        settings.SystemPrompt = "Custom prompt";
        settings.DpdfNetDownloadUrl = "https://example.com/model.onnx";

        Assert.Equal(8u, settings.Concurrency);
        Assert.False(settings.UseContextTranslation);
        Assert.Equal(20u, settings.ContextSize);
        Assert.True(settings.ApplyDpdfNet);
        Assert.True(settings.UseVoiceActivityDetection);
        Assert.Equal("Custom prompt", settings.SystemPrompt);
        Assert.Equal("https://example.com/model.onnx", settings.DpdfNetDownloadUrl);
    }
}

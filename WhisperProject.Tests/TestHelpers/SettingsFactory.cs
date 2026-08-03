using WhisperProject.Models;

namespace WhisperProject.Tests.TestHelpers;

/// <summary>
/// Creates a fully populated <see cref="Settings"/> instance with all
/// <c>required</c> members filled with sensible defaults.
/// </summary>
public static class SettingsFactory
{
    public static Settings Create()
    {
        return new Settings
        {
            InputPath = @"C:\input",
            TargetLanguage = "English",
            ApiKey = "",
            Url = "http://127.0.0.1",
            Port = 1234,
            GptPath = "/v1",
            GptModel = "test-model",
            WhisperModelpath = ""
        };
    }
}

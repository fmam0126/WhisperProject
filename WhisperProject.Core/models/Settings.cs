namespace WhisperProject.Models;

public sealed class Settings
{
    public required string InputPath { get; set; } = string.Empty;
    public required string TargetLanguage { get; set; } = string.Empty;
    public required string ApiKey { get; set; } = string.Empty;
    public required string Url { get; set; } = string.Empty;
    public required int Port { get; set; }
    public required string GptPath { get; set; } = string.Empty;
    public required string GptModel { get; set; } = string.Empty;
    public uint Concurrency { get; set; } = 4;
    public bool UseContextTranslation { get; set; } = true;
    public uint ContextSize { get; set; } = 10;
    public string SystemPrompt { get; set; } = "You are a helpful assistant for translating video subtitles. You receive text in the format of a subtitle file and you translate it to the target language without adding any comments or explanations, just output the translated text. Always keep the formatting of the original text.";
    public required string WhisperModelPath { get; set; } = string.Empty;
    // public required string WhisperModel { get; set; } = string.Empty;
    public string WhisperLanguage { get; set; } = string.Empty;
    public bool ApplyDpdfNet { get; set; } = false;
    public string DpdfNetModelPath { get; set; } = string.Empty;
    public string DpdfNetDownloadUrl { get; set; } = "https://github.com/k2-fsa/sherpa-onnx/releases/download/speech-enhancement-models/dpdfnet8.onnx";
    public bool UseVoiceActivityDetection { get; set; } = false;
}
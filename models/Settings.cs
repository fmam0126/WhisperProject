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
    public required string WhisperModelpath { get; set; } = string.Empty;
    public required string WhisperModel { get; set; } = string.Empty;
    public string WhisperLanguage { get; set; } = null!;
    public bool ApplyVoiceEmphasisFilter { get; set; } = false;
    public string DpdfNetModelPath { get; set; } = string.Empty;
    public bool UseVoiceActivityDetection { get; set; } = false;
}
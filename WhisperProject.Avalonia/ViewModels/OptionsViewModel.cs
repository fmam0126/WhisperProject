using System;
using WhisperProject.Models;

namespace WhisperProject.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the Options window. Wraps all transcription/translation settings
/// with two-way binding support. Radio-button groups control mutually exclusive
/// boolean options.
/// </summary>
public class OptionsViewModel : ViewModelBase
{
    // ── API / LLM settings ─────────────────────────────────────────────

    private string _url = "http://127.0.0.1";
    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    /// <summary>Port as a string so TextBox binding works with compiled bindings.</summary>
    private string _port = "1234";
    public string Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    private string _apiKey = string.Empty;
    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    private string _gptPath = "/v1";
    public string GptPath
    {
        get => _gptPath;
        set => SetProperty(ref _gptPath, value);
    }

    private string _gptModel = "google/gemma-4-e4b";
    public string GptModel
    {
        get => _gptModel;
        set => SetProperty(ref _gptModel, value);
    }

    // ── Target language ─────────────────────────────────────────────────

    private string _targetLanguage = "English";
    public string TargetLanguage
    {
        get => _targetLanguage;
        set => SetProperty(ref _targetLanguage, value);
    }

    // ── Whisper settings ─────────────────────────────────────────────────

    private string _whisperModelPath = string.Empty;
    public string WhisperModelPath
    {
        get => _whisperModelPath;
        set => SetProperty(ref _whisperModelPath, value);
    }

    private string _whisperLanguage = string.Empty;
    public string WhisperLanguage
    {
        get => _whisperLanguage;
        set => SetProperty(ref _whisperLanguage, value);
    }

    // ── Concurrency / context ───────────────────────────────────────────

    /// <summary>Concurrency as a string so TextBox binding works with compiled bindings.</summary>
    private string _concurrency = "4";
    public string Concurrency
    {
        get => _concurrency;
        set => SetProperty(ref _concurrency, value);
    }

    /// <summary>Context size as a string so TextBox binding works with compiled bindings.</summary>
    private string _contextSize = "10";
    public string ContextSize
    {
        get => _contextSize;
        set => SetProperty(ref _contextSize, value);
    }

    // ── System prompt ───────────────────────────────────────────────────

    private string _systemPrompt = "You are a helpful assistant for translating video subtitles. You receive text in the format of a subtitle file and you translate it to the target language without adding any comments or explanations, just output the translated text. Always keep the formatting of the original text.";
    public string SystemPrompt
    {
        get => _systemPrompt;
        set => SetProperty(ref _systemPrompt, value);
    }

    // ── Radio-button groups (mutually exclusive booleans) ───────────────
    //
    //  RadioButton in Avalonia doesn't natively bind to an enum or
    //  automatically uncheck siblings. We emulate the behaviour with three
    //  bool properties per group where _exactly one_ is true at all times.
    //
    //  Group 1 – Translation mode
    //  Group 2 – Voice Activity Detection
    //  Group 3 – DpdfNet voice enhancement

    // ── Translation mode ────────────────────────────────────────────────

    private bool _useContextTranslation = true;
    public bool UseContextTranslation
    {
        get => _useContextTranslation;
        set
        {
            if (SetProperty(ref _useContextTranslation, value) && value)
            {
                // Mutually exclusive: turn off the other
                UsePerLineTranslation = false;
            }
        }
    }

    private bool _usePerLineTranslation;
    public bool UsePerLineTranslation
    {
        get => _usePerLineTranslation;
        set
        {
            if (SetProperty(ref _usePerLineTranslation, value) && value)
            {
                UseContextTranslation = false;
            }
        }
    }

    // ── Voice Activity Detection ────────────────────────────────────────

    private bool _vadEnabled;
    public bool VadEnabled
    {
        get => _vadEnabled;
        set
        {
            if (SetProperty(ref _vadEnabled, value) && value)
            {
                VadDisabled = false;
            }
        }
    }

    private bool _vadDisabled = true;
    public bool VadDisabled
    {
        get => _vadDisabled;
        set
        {
            if (SetProperty(ref _vadDisabled, value) && value)
            {
                VadEnabled = false;
            }
        }
    }

    // ── DpdfNet voice enhancement ───────────────────────────────────────

    private bool _dpdfNetEnabled;
    public bool DpdfNetEnabled
    {
        get => _dpdfNetEnabled;
        set
        {
            if (SetProperty(ref _dpdfNetEnabled, value) && value)
            {
                DpdfNetDisabled = false;
            }
        }
    }

    private bool _dpdfNetDisabled = true;
    public bool DpdfNetDisabled
    {
        get => _dpdfNetDisabled;
        set
        {
            if (SetProperty(ref _dpdfNetDisabled, value) && value)
            {
                DpdfNetEnabled = false;
            }
        }
    }

    // ── DpdfNet model path ──────────────────────────────────────────────

    private string _dpdfNetModelPath = string.Empty;
    public string DpdfNetModelPath
    {
        get => _dpdfNetModelPath;
        set => SetProperty(ref _dpdfNetModelPath, value);
    }

    private string _dpdfNetDownloadUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/speech-enhancement-models/dpdfnet8.onnx";
    public string DpdfNetDownloadUrl
    {
        get => _dpdfNetDownloadUrl;
        set => SetProperty(ref _dpdfNetDownloadUrl, value);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    /// <summary>Parses Port to int, defaulting to 1234 on bad input.</summary>
    private int ParsePort() =>
        int.TryParse(Port, out var v) && v is >= 1 and <= 65535 ? v : 1234;

    /// <summary>Parses Concurrency to uint, defaulting to 4 on bad input.</summary>
    private uint ParseConcurrency() =>
        uint.TryParse(Concurrency, out var v) && v >= 1 ? v : 4;

    /// <summary>Parses ContextSize to uint, defaulting to 10 on bad input.</summary>
    private uint ParseContextSize() =>
        uint.TryParse(ContextSize, out var v) && v >= 1 ? v : 10;

    // ── Load / Save ─────────────────────────────────────────────────────

    /// <summary>
    /// Populates the ViewModel from a Settings model (e.g. loaded from appsettings.json).
    /// </summary>
    public void LoadFromSettings(Settings s)
    {
        Url = s.Url;
        Port = s.Port.ToString();
        ApiKey = s.ApiKey;
        GptPath = s.GptPath;
        GptModel = s.GptModel;
        TargetLanguage = s.TargetLanguage;
        WhisperModelPath = s.WhisperModelpath;
        WhisperLanguage = s.WhisperLanguage ?? string.Empty;
        Concurrency = s.Concurrency.ToString();
        ContextSize = s.ContextSize.ToString();
        SystemPrompt = s.SystemPrompt;
        DpdfNetModelPath = s.DpdfNetModelPath;
        DpdfNetDownloadUrl = s.DpdfNetDownloadUrl;

        // Radio groups
        UseContextTranslation = s.UseContextTranslation;
        UsePerLineTranslation = !s.UseContextTranslation;
        VadEnabled = s.UseVoiceActivityDetection;
        VadDisabled = !s.UseVoiceActivityDetection;
        DpdfNetEnabled = s.ApplyDpdfNet;
        DpdfNetDisabled = !s.ApplyDpdfNet;
    }

    /// <summary>
    /// Builds a Settings model from the current ViewModel state.
    /// </summary>
    public Settings ToSettings(string inputPath)
    {
        return new Settings
        {
            InputPath = inputPath,
            Url = Url,
            Port = ParsePort(),
            ApiKey = ApiKey,
            GptPath = GptPath,
            GptModel = GptModel,
            TargetLanguage = TargetLanguage,
            WhisperModelpath = WhisperModelPath,
            WhisperLanguage = WhisperLanguage,
            Concurrency = ParseConcurrency(),
            ContextSize = ParseContextSize(),
            SystemPrompt = SystemPrompt,
            UseContextTranslation = UseContextTranslation,
            UseVoiceActivityDetection = VadEnabled,
            ApplyDpdfNet = DpdfNetEnabled,
            DpdfNetModelPath = DpdfNetModelPath,
            DpdfNetDownloadUrl = DpdfNetDownloadUrl
        };
    }
}

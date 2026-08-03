using WhisperProject.Models;

namespace WhisperProject.Avalonia.ViewModels;

/// <summary>
/// ViewModel for the Options window. Wraps all transcription and translation
/// settings with two-way binding support. Radio-button groups control mutually
/// exclusive boolean options.
/// </summary>
public class OptionsViewModel : ViewModelBase
{
    // API / LLM settings

    private string _url = "http://127.0.0.1";

    /// <summary>
    /// The base URL of the OpenAI-compatible API endpoint (e.g. Ollama, LM Studio).
    /// </summary>
    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    /// <summary>
    /// The port number for the API endpoint, stored as a string for seamless
    /// TextBox binding with compiled bindings.
    /// </summary>
    private string _port = "1234";

    /// <inheritdoc cref="_port"/>
    public string Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    private string _apiKey = string.Empty;

    /// <summary>
    /// Optional API key for the LLM endpoint. Leave empty for local LLMs that
    /// do not require authentication.
    /// </summary>
    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    private string _gptPath = "/v1";

    /// <summary>
    /// The base path for the OpenAI-compatible API (e.g. "/v1").
    /// The SDK appends "/chat/completions" automatically.
    /// </summary>
    public string GptPath
    {
        get => _gptPath;
        set => SetProperty(ref _gptPath, value);
    }

    private string _gptModel = "google/gemma-4-e2b";

    /// <summary>
    /// The model name to use for subtitle translation (e.g. "gpt-4o", "llama2-13b-chat").
    /// </summary>
    public string GptModel
    {
        get => _gptModel;
        set => SetProperty(ref _gptModel, value);
    }

    // Target language 

    private string _targetLanguage = "English";

    /// <summary>
    /// The language to translate subtitles into (e.g. "English", "Norwegian", "Spanish").
    /// </summary>
    public string TargetLanguage
    {
        get => _targetLanguage;
        set => SetProperty(ref _targetLanguage, value);
    }

    // Whisper settings 

    private string _whisperModelPath = string.Empty;

    /// <summary>
    /// File-system path to the Whisper GGML model file (e.g. "ggml-largev2.bin").
    /// Leave empty to auto-download the default model.
    /// </summary>
    public string WhisperModelPath
    {
        get => _whisperModelPath;
        set => SetProperty(ref _whisperModelPath, value);
    }

    private string _whisperLanguage = string.Empty;

    /// <summary>
    /// Language hint for Whisper transcription (e.g. "en", "no").
    /// Leave empty for automatic language detection.
    /// </summary>
    public string WhisperLanguage
    {
        get => _whisperLanguage;
        set => SetProperty(ref _whisperLanguage, value);
    }

    // Concurrency / context 

    /// <summary>
    /// Maximum number of concurrent translation requests, stored as a string
    /// for seamless TextBox binding with compiled bindings.
    /// </summary>
    private string _concurrency = "4";

    /// <inheritdoc cref="_concurrency"/>
    public string Concurrency
    {
        get => _concurrency;
        set => SetProperty(ref _concurrency, value);
    }

    /// <summary>
    /// Number of subtitle entries to send to the LLM per batch during
    /// context-aware translation, stored as a string for TextBox binding.
    /// </summary>
    private string _contextSize = "10";

    /// <inheritdoc cref="_contextSize"/>
    public string ContextSize
    {
        get => _contextSize;
        set => SetProperty(ref _contextSize, value);
    }

    // System prompt 

    private string _systemPrompt =
        "You are a helpful assistant for translating video subtitles. " +
        "You receive text in the format of a subtitle file and you translate " +
        "it to the target language without adding any comments or explanations, " +
        "just output the translated text. Always keep the formatting of the " +
        "original text.";

    /// <summary>
    /// The system prompt that instructs the LLM how to perform subtitle translation.
    /// </summary>
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

    /// <summary>
    /// When true, subtitles are translated in batches with surrounding context
    /// for better quality. Mutually exclusive with <see cref="UsePerLineTranslation"/>.
    /// </summary>
    public bool UseContextTranslation
    {
        get => _useContextTranslation;
        set
        {
            if (SetProperty(ref _useContextTranslation, value) && value)
                UsePerLineTranslation = false;
        }
    }

    private bool _usePerLineTranslation;

    /// <summary>
    /// When true, each subtitle line is translated independently.
    /// Mutually exclusive with <see cref="UseContextTranslation"/>.
    /// </summary>
    public bool UsePerLineTranslation
    {
        get => _usePerLineTranslation;
        set
        {
            if (SetProperty(ref _usePerLineTranslation, value) && value)
                UseContextTranslation = false;
        }
    }

    // ── Voice Activity Detection ────────────────────────────────────────

    private bool _vadEnabled = true;

    /// <summary>
    /// When true, Voice Activity Detection filters out silence before
    /// transcription. Mutually exclusive with <see cref="VadDisabled"/>.
    /// </summary>
    public bool VadEnabled
    {
        get => _vadEnabled;
        set
        {
            if (SetProperty(ref _vadEnabled, value) && value)
                VadDisabled = false;
        }
    }

    private bool _vadDisabled;

    /// <summary>
    /// When true, the entire audio file is processed without silence filtering.
    /// Mutually exclusive with <see cref="VadEnabled"/>.
    /// </summary>
    public bool VadDisabled
    {
        get => _vadDisabled;
        set
        {
            if (SetProperty(ref _vadDisabled, value) && value)
                VadEnabled = false;
        }
    }

    // ── DpdfNet voice enhancement ───────────────────────────────────────

    private bool _dpdfNetEnabled;

    /// <summary>
    /// When true, DpdfNet AI-based speech denoising is applied before
    /// transcription. Mutually exclusive with <see cref="DpdfNetDisabled"/>.
    /// </summary>
    public bool DpdfNetEnabled
    {
        get => _dpdfNetEnabled;
        set
        {
            if (SetProperty(ref _dpdfNetEnabled, value) && value)
                DpdfNetDisabled = false;
        }
    }

    private bool _dpdfNetDisabled = true;

    /// <summary>
    /// When true, no voice enhancement is applied.
    /// Mutually exclusive with <see cref="DpdfNetEnabled"/>.
    /// </summary>
    public bool DpdfNetDisabled
    {
        get => _dpdfNetDisabled;
        set
        {
            if (SetProperty(ref _dpdfNetDisabled, value) && value)
                DpdfNetEnabled = false;
        }
    }

    // ── DpdfNet model path ──────────────────────────────────────────────

    private string _dpdfNetModelPath = string.Empty;

    /// <summary>
    /// File-system path to the DpdfNet ONNX model. Leave empty to auto-download
    /// from <see cref="DpdfNetDownloadUrl"/>.
    /// </summary>
    public string DpdfNetModelPath
    {
        get => _dpdfNetModelPath;
        set => SetProperty(ref _dpdfNetModelPath, value);
    }

    private string _dpdfNetDownloadUrl =
        "https://github.com/k2-fsa/sherpa-onnx/releases/download/speech-enhancement-models/dpdfnet8.onnx";

    /// <summary>
    /// URL used to auto-download the DpdfNet ONNX model when no local model
    /// file is found.
    /// </summary>
    public string DpdfNetDownloadUrl
    {
        get => _dpdfNetDownloadUrl;
        set => SetProperty(ref _dpdfNetDownloadUrl, value);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <see cref="Port"/> to an integer, defaulting to 1234 on invalid input.
    /// </summary>
    private int ParsePort() =>
        int.TryParse(Port, out var value) && value is >= 1 and <= 65535 ? value : 1234;

    /// <summary>
    /// Parses <see cref="Concurrency"/> to an unsigned integer, defaulting to 4
    /// on invalid input.
    /// </summary>
    private uint ParseConcurrency() =>
        uint.TryParse(Concurrency, out var value) && value >= 1 ? value : 4;

    /// <summary>
    /// Parses <see cref="ContextSize"/> to an unsigned integer, defaulting to 10
    /// on invalid input.
    /// </summary>
    private uint ParseContextSize() =>
        uint.TryParse(ContextSize, out var value) && value >= 1 ? value : 10;

    // ── Load / Save ─────────────────────────────────────────────────────

    /// <summary>
    /// Populates the ViewModel from a <see cref="Settings"/> model
    /// (e.g. loaded from <c>appsettings.json</c>).
    /// </summary>
    /// <param name="settings">The settings model to read values from.</param>
    public void LoadFromSettings(Settings settings)
    {
        Url = settings.Url;
        Port = settings.Port.ToString();
        ApiKey = settings.ApiKey;
        GptPath = settings.GptPath;
        GptModel = settings.GptModel;
        TargetLanguage = settings.TargetLanguage;
        WhisperModelPath = settings.WhisperModelpath;
        WhisperLanguage = settings.WhisperLanguage ?? string.Empty;
        Concurrency = settings.Concurrency.ToString();
        ContextSize = settings.ContextSize.ToString();
        SystemPrompt = settings.SystemPrompt;
        DpdfNetModelPath = settings.DpdfNetModelPath;
        DpdfNetDownloadUrl = settings.DpdfNetDownloadUrl;

        // Radio groups — mirror the boolean values into the paired radio properties
        UseContextTranslation = settings.UseContextTranslation;
        UsePerLineTranslation = !settings.UseContextTranslation;
        VadEnabled = settings.UseVoiceActivityDetection;
        VadDisabled = !settings.UseVoiceActivityDetection;
        DpdfNetEnabled = settings.ApplyDpdfNet;
        DpdfNetDisabled = !settings.ApplyDpdfNet;
    }

    /// <summary>
    /// Builds a <see cref="Settings"/> model from the current ViewModel state,
    /// parsing numeric strings into their typed equivalents.
    /// </summary>
    /// <param name="inputPath">
    /// The file or folder path to process. This is set by the main window rather
    /// than the options dialog, so it is passed in here rather than stored as
    /// a ViewModel property.
    /// </param>
    /// <returns>A populated <see cref="Settings"/> instance ready for the pipeline.</returns>
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

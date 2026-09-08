using System.Text;
using WhisperProject.Class;
using WhisperProject.Core;
using WhisperProject.Tests.TestHelpers;

namespace WhisperProject.Tests.Core;

/// <summary>
/// End-to-end "smoke" tests for <see cref="WhisperClient"/> that run the real
/// Whisper model over a real speech clip (<c>TestData/kennedy.wav</c>).
/// <para>
/// A small English-only model (<c>ggml-base.en</c>, ~141 MB) is auto-downloaded
/// once into the OS temp folder and cached between runs so local iterations are
/// fast. The clip is copied into a temp directory because
/// <see cref="WhisperClient"/> writes the resulting <c>.srt</c> next to the input.
/// </para>
/// <para>
/// These tests exercise the native Whisper runtime and therefore require network
/// access on the first run (model download) and the <c>kennedy.wav</c> fixture
/// to be present in the test output (see the test project file).
/// </para>
/// </summary>
public class WhisperClientSmokeTests
{
    private const string ModelUrl =
        "https://huggingface.co/sandrohanea/whisper.net/resolve/v4/classic/ggml-base.en.bin";

    private const long ModelMinBytes = 100L * 1024 * 1024;

    private const string KennedyWavName = "kennedy.wav";

    // Guard so the two tests (which xUnit runs sequentially within this class)
    // and repeated runs all reuse a single download.
    private static readonly SemaphoreSlim ModelLock = new(1, 1);
    private static string? _cachedModelPath;

    /// <summary>Full path to the committed test clip in the output directory.</summary>
    private static string SourceKennedyWav =>
        Path.Combine(AppContext.BaseDirectory, "TestData", KennedyWavName);

    [Fact]
    public async Task TranscribeAsyncProducesSrtForKennedyClip()
    {
        using var dir = new TempDir();
        var wav = CopyClipInto(dir);
        var model = await EnsureModelAsync();

        // ggml-base.en is an English-only model, so the language is pinned to "en"
        // (auto-detection is unreliable on such a short clip).
        var language = await WhisperClient.TranscribeAsync(wav, modelPath: model, language: "en");

        Assert.Equal("en", language, ignoreCase: true);
        var srt = Path.ChangeExtension(wav, ".srt");
        Assert.True(File.Exists(srt), $"Expected WhisperClient to write an SRT file at {srt}.");

        var transcript = ReadSrtText(srt);
        Assert.False(string.IsNullOrWhiteSpace(transcript),
            $"Expected a non-empty transcript in {srt}.");
        // Kennedy's "We choose to go to the Moon" speech.
        Assert.Contains("moon", transcript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TranscribeVadAsyncProducesSrtForKennedyClip()
    {
        using var dir = new TempDir();
        var wav = CopyClipInto(dir);
        var model = await EnsureModelAsync();

        // ggml-base.en is an English-only model, so the language is pinned to "en".
        var language = await WhisperClient.TranscribeVadAsync(wav, modelPath: model, language: "en");

        Assert.Equal("en", language, ignoreCase: true);
        var srt = Path.ChangeExtension(wav, ".srt");
        Assert.True(File.Exists(srt), $"Expected WhisperClient to write an SRT file at {srt}.");

        var transcript = ReadSrtText(srt);
        Assert.False(string.IsNullOrWhiteSpace(transcript),
            $"Expected a non-empty transcript in {srt}.");
        // Kennedy's "We choose to go to the Moon" speech.
        Assert.Contains("moon", transcript, StringComparison.OrdinalIgnoreCase);
    }

    private static string CopyClipInto(TempDir dir)
    {
        Assert.True(File.Exists(SourceKennedyWav),
            $"Test fixture missing: {SourceKennedyWav}. Ensure kennedy.wav exists in " +
            "WhisperProject.Tests/TestData (it is copied to the output on build).");

        var wav = Path.Combine(dir.Path, KennedyWavName);
        File.Copy(SourceKennedyWav, wav);
        return wav;
    }

    /// <summary>
    /// Returns the path of a Whisper GGML model, downloading <c>ggml-base.en</c>
    /// once (into the OS temp folder) if it is not already cached.
    /// </summary>
    private static async Task<string> EnsureModelAsync()
    {
        if (_cachedModelPath is not null && File.Exists(_cachedModelPath))
            return _cachedModelPath;

        await ModelLock.WaitAsync();
        try
        {
            if (_cachedModelPath is not null && File.Exists(_cachedModelPath))
                return _cachedModelPath;

            var modelsDir = Path.Combine(Path.GetTempPath(), "whisperproject-smoke-models");
            Directory.CreateDirectory(modelsDir);
            var modelPath = Path.Combine(modelsDir, "ggml-base.en.bin");

            if (!File.Exists(modelPath) || new FileInfo(modelPath).Length < ModelMinBytes)
                await ModelDownloader.DownloadAsync(ModelUrl, modelPath);

            _cachedModelPath = modelPath;
            return modelPath;
        }
        finally
        {
            ModelLock.Release();
        }
    }

    /// <summary>
    /// Reads an SRT file back into a single transcript string, stripping
    /// subtitle indices and timecodes.
    /// </summary>
    private static string ReadSrtText(string srtPath)
    {
        var sb = new StringBuilder();
        foreach (var line in File.ReadLines(srtPath))
        {
            if (line.Contains("-->") || string.IsNullOrWhiteSpace(line))
                continue;
            if (int.TryParse(line, out _))
                continue;
            sb.Append(' ').Append(line.Trim());
        }
        return sb.ToString().Trim();
    }
}

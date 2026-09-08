using System.Text;
using WhisperProject.Core;
using WhisperProject.Tests.TestHelpers;

namespace WhisperProject.Tests.Core;

/// <summary>
/// End-to-end "smoke" test for <see cref="Qwen3Asr"/> that runs the real
/// sherpa-onnx Qwen3 ASR model over the same speech clip used by the Whisper
/// smoke tests. The Qwen3 pipeline requires mono 16 kHz input (its
/// <c>WaveReader</c> rejects multi-channel files), so it uses the mono
/// down-mix <c>TestData/kennedy_mono.wav</c> derived from <c>kennedy.wav</c>.
/// <para>
/// <see cref="Qwen3Asr.RunQwen3Asr"/> auto-downloads the Qwen3 0.6B int8 model,
/// a Silero VAD model and a whisper-tiny language-detection model into the
/// model directory on first use. To avoid re-downloading ~1.2 GB on every run,
/// the model directory is cached under the OS temp folder and can be pointed at
/// an existing download with the <c>QWEN3_MODEL_DIR</c> environment variable.
/// </para>
/// </summary>
public class Qwen3AsrSmokeTests
{
    /// <summary>Env var override pointing at a directory that already contains the models.</summary>
    private const string ModelDirEnvVar = "QWEN3_MODEL_DIR";

    private const string KennedyWavName = "kennedy_mono.wav";

    private static readonly SemaphoreSlim ModelDirLock = new(1, 1);
    private static string? _cachedModelDir;

    /// <summary>Full path to the committed test clip in the output directory.</summary>
    private static string SourceKennedyWav =>
        Path.Combine(AppContext.BaseDirectory, "TestData", KennedyWavName);

    [Fact]
    public async Task RunQwen3AsrProducesSrtForKennedyClip()
    {
        Assert.True(File.Exists(SourceKennedyWav),
            $"Test fixture missing: {SourceKennedyWav}. Ensure kennedy_mono.wav exists in " +
            "WhisperProject.Tests/TestData (it is copied to the output on build).");

        using var dir = new TempDir();
        var wav = Path.Combine(dir.Path, KennedyWavName);
        File.Copy(SourceKennedyWav, wav);
        var modelDir = await EnsureModelDirAsync();

        var language = await Qwen3Asr.RunQwen3Asr(wav, modelDir);

        Assert.Equal("en", language, ignoreCase: true);
        var srt = Path.ChangeExtension(wav, ".srt");
        Assert.True(File.Exists(srt), $"Expected Qwen3Asr to write an SRT file at {srt}.");

        var transcript = ReadSrtText(srt);
        Assert.False(string.IsNullOrWhiteSpace(transcript),
            $"Expected a non-empty transcript in {srt}.");
        // Kennedy's "We choose to go to the Moon" speech.
        Assert.Contains("moon", transcript, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves the Qwen3 model directory: an existing directory from the
    /// <c>QWEN3_MODEL_DIR</c> env var, otherwise a cached folder under the OS
    /// temp path that is populated by <see cref="Qwen3Asr"/> on first use.
    /// </summary>
    private static async Task<string> EnsureModelDirAsync()
    {
        if (_cachedModelDir is not null)
            return _cachedModelDir;

        await ModelDirLock.WaitAsync();
        try
        {
            if (_cachedModelDir is not null)
                return _cachedModelDir;

            var overrideDir = Environment.GetEnvironmentVariable(ModelDirEnvVar);
            var modelDir = string.IsNullOrWhiteSpace(overrideDir)
                ? Path.Combine(Path.GetTempPath(), "whisperproject-smoke-models", "qwen3-asr")
                : Path.GetFullPath(overrideDir);

            Directory.CreateDirectory(modelDir);
            _cachedModelDir = modelDir;
            return modelDir;
        }
        finally
        {
            ModelDirLock.Release();
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

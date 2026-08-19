using WhisperProject.Core;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for the pure-logic path helper in <see cref="Qwen3Asr"/>.
/// </summary>
public class Qwen3AsrTests
{
    [Fact]
    public void GetWhisperTinyModelPathBuildsLocalPathsUnderModelDir()
    {
        var modelDir = @"C:\models";

        var encoderPath = Qwen3Asr.GetWhisperTinyModelPath(modelDir, "tiny-encoder.int8.onnx");
        var decoderPath = Qwen3Asr.GetWhisperTinyModelPath(modelDir, "tiny-decoder.int8.onnx");

        Assert.Equal(
            System.IO.Path.Combine(modelDir, "sherpa-onnx-whisper-tiny", "tiny-encoder.int8.onnx"),
            encoderPath);
        Assert.Equal(
            System.IO.Path.Combine(modelDir, "sherpa-onnx-whisper-tiny", "tiny-decoder.int8.onnx"),
            decoderPath);

        // Regression guard: paths must be local (under modelDir), never built
        // from a download URL.
        Assert.StartsWith(modelDir, encoderPath);
        Assert.StartsWith(modelDir, decoderPath);
    }
}

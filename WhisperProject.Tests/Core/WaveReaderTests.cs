using WhisperProject.Class;
using WhisperProject.Tests.TestHelpers;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for <see cref="WaveReader"/> using synthesised WAV files.
/// </summary>
public class WaveReaderTests
{
    [Fact]
    public void ConstructorValidMonoWavReadsSampleRateAndSamples()
    {
        short[] inputSamples = [0, 16384, -32768, 32767];
        var wav = WaveFixture.BuildWav(16000, inputSamples);

        using var dir = new TempDir();
        var path = WaveFixture.WriteTempWav(dir, "test.wav", wav);

        var reader = new WaveReader(path);

        Assert.Equal(16000, reader.SampleRate);
        Assert.Equal(4, reader.Samples.Length);
        Assert.Equal(0f, reader.Samples[0]);
        Assert.Equal(0.5f, reader.Samples[1]);
        Assert.Equal(-1f, reader.Samples[2]);
        // 32767 / 32768 ≈ 0.99997 — use precision overload
        Assert.Equal(32767f / 32768f, reader.Samples[3], 6);
    }

    [Fact]
    public void ConstructorMissingFileThrowsApplicationException()
    {
        var missingPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"no-such-file-{Guid.NewGuid()}.wav");

        var ex = Assert.Throws<ApplicationException>(
            () => new WaveReader(missingPath));

        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public void ConstructorInvalidMagicBytesThrowsApplicationException()
    {
        short[] samples = [0, 100];
        var wav = WaveFixture.BuildNonRiff(samples);

        using var dir = new TempDir();
        var path = WaveFixture.WriteTempWav(dir, "bad.wav", wav);

        var ex = Assert.Throws<ApplicationException>(
            () => new WaveReader(path));

        Assert.Contains("Invalid", ex.Message);
    }

    [Fact]
    public void ConstructorStereoWavThrowsApplicationException()
    {
        short[] samples = [0, 100, 200, 300]; // even count for stereo
        var wav = WaveFixture.BuildWav(16000, samples, numChannels: 2);

        using var dir = new TempDir();
        var path = WaveFixture.WriteTempWav(dir, "stereo.wav", wav);

        Assert.Throws<ApplicationException>(() => new WaveReader(path));
    }

    [Fact]
    public void Constructor8BitWavThrowsApplicationException()
    {
        short[] samples = [0, 100];
        var wav = WaveFixture.BuildWav(16000, samples,
            numChannels: 1, bitsPerSample: 8);

        using var dir = new TempDir();
        var path = WaveFixture.WriteTempWav(dir, "8bit.wav", wav);

        Assert.Throws<ApplicationException>(() => new WaveReader(path));
    }

    [Fact]
    public void ConstructorWithListMetadataChunkBeforeDataStillReadsSamples()
    {
        short[] inputSamples = [42, -42];
        var wav = WaveFixture.BuildWavWithListChunk(16000, inputSamples);

        using var dir = new TempDir();
        var path = WaveFixture.WriteTempWav(dir, "list.wav", wav);

        var reader = new WaveReader(path);

        Assert.Equal(16000, reader.SampleRate);
        Assert.Equal(2, reader.Samples.Length);
        Assert.Equal(42f / 32768f, reader.Samples[0], 6);
        Assert.Equal(-42f / 32768f, reader.Samples[1], 6);
    }

    [Fact]
    public void Constructor44100HzMonoWavValid()
    {
        short[] inputSamples = [100, 200];
        var wav = WaveFixture.BuildWav(44100, inputSamples);

        using var dir = new TempDir();
        var path = WaveFixture.WriteTempWav(dir, "44k.wav", wav);

        var reader = new WaveReader(path);

        Assert.Equal(44100, reader.SampleRate);
        Assert.Equal(2, reader.Samples.Length);
    }

    [Fact]
    public void ConstructorEmptySampleDataReturnsEmptySamplesArray()
    {
        var wav = WaveFixture.BuildWav(16000, []);

        using var dir = new TempDir();
        var path = WaveFixture.WriteTempWav(dir, "empty.wav", wav);

        var reader = new WaveReader(path);

        Assert.Empty(reader.Samples);
        Assert.Equal(16000, reader.SampleRate);
    }
}

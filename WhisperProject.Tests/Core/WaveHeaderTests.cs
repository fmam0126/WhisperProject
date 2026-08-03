using WhisperProject.Class;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for <see cref="WaveHeader.Validate"/>.
/// The struct is constructed inline — no file I/O required.
/// </summary>
public class WaveHeaderTests
{
    private const int RIFF = 0x46464952;
    private const int WAVE = 0x45564157;
    private const int FMT  = 0x20746D66;
    private const int DATA = 0x61746164;

    /// <summary>Returns a valid 16000 Hz mono 16-bit PCM header.</summary>
    private static WaveHeader ValidHeader() => new()
    {
        ChunkID       = RIFF,
        ChunkSize     = 36 + 100,          // placeholder data size
        Format        = WAVE,
        SubChunk1ID   = FMT,
        SubChunk1Size = 16,
        AudioFormat   = 1,
        NumChannels   = 1,
        SampleRate    = 16000,
        ByteRate      = 16000 * 1 * 16 / 8,  // 32000
        BlockAlign    = 1 * 16 / 8,           // 2
        BitsPerSample = 16,
        SubChunk2ID   = DATA,
        SubChunk2Size = 100
    };

    [Fact]
    public void Validate_ValidHeader_ReturnsTrue()
    {
        Assert.True(ValidHeader().Validate());
    }

    [Fact]
    public void Validate_44100HzMono16Bit_ReturnsTrue()
    {
        var header = ValidHeader();
        header.SampleRate = 44100;
        header.ByteRate = 44100 * 1 * 16 / 8;
        header.BlockAlign = 1 * 16 / 8;
        Assert.True(header.Validate());
    }

    [Theory]
    [InlineData("ChunkID")]
    [InlineData("Format")]
    [InlineData("SubChunk1ID")]
    public void Validate_BadMagicConstants_ReturnsFalse(string field)
    {
        var header = ValidHeader();
        switch (field)
        {
            case "ChunkID":     header.ChunkID = 0x00000000; break;
            case "Format":      header.Format = 0x00000000; break;
            case "SubChunk1ID": header.SubChunk1ID = 0x00000000; break;
        }
        Assert.False(header.Validate());
    }

    [Fact]
    public void Validate_SubChunk1SizeNot16_ReturnsFalse()
    {
        var header = ValidHeader();
        header.SubChunk1Size = 12;
        Assert.False(header.Validate());
    }

    [Fact]
    public void Validate_NonPcmAudioFormat_ReturnsFalse()
    {
        var header = ValidHeader();
        header.AudioFormat = 3; // IEEE float
        Assert.False(header.Validate());
    }

    [Fact]
    public void Validate_Stereo_ReturnsFalse()
    {
        var header = ValidHeader();
        header.NumChannels = 2;
        header.ByteRate = 16000 * 2 * 16 / 8;
        header.BlockAlign = 2 * 16 / 8;
        Assert.False(header.Validate());
    }

    [Fact]
    public void Validate_WrongByteRate_ReturnsFalse()
    {
        var header = ValidHeader();
        header.ByteRate = 16000; // should be 32000
        Assert.False(header.Validate());
    }

    [Fact]
    public void Validate_WrongBlockAlign_ReturnsFalse()
    {
        var header = ValidHeader();
        header.BlockAlign = 4; // should be 2
        Assert.False(header.Validate());
    }

    [Fact]
    public void Validate_BitsPerSampleNot16_ReturnsFalse()
    {
        var header = ValidHeader();
        header.BitsPerSample = 8;
        header.ByteRate = 16000 * 1 * 8 / 8;
        header.BlockAlign = 1 * 8 / 8;
        Assert.False(header.Validate());
    }
}

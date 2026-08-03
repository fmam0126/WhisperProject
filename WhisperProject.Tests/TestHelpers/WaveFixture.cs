using System.Text;

namespace WhisperProject.Tests.TestHelpers;

/// <summary>
/// Synthesises WAV byte arrays matching the 44-byte sequential struct layout
/// that <c>WaveReader</c> reads via <c>Marshal.PtrToStructure</c>.
/// </summary>
public static class WaveFixture
{
    // FourCC constants stored as little-endian ints
    public const int RIFF = 0x46464952;
    public const int WAVE = 0x45564157;
    public const int FMT  = 0x20746D66;
    public const int DATA = 0x61746164;
    public const int LIST = 0x5453494C;

    /// <summary>
    /// Builds a standard PCM WAV (44-byte header + sample data).
    /// </summary>
    public static byte[] BuildWav(int sampleRate, short[] samples,
        int numChannels = 1, short bitsPerSample = 16)
    {
        int dataSize = samples.Length * sizeof(short);
        short blockAlign = (short)(numChannels * bitsPerSample / 8);
        int byteRate = sampleRate * numChannels * bitsPerSample / 8;
        int fileSize = 36 + dataSize; // ChunkSize = 36 + SubChunk2Size

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write(RIFF);                    // ChunkID
        bw.Write(fileSize);                // ChunkSize
        bw.Write(WAVE);                    // Format

        // fmt sub-chunk
        bw.Write(FMT);                     // SubChunk1ID
        bw.Write(16);                      // SubChunk1Size
        bw.Write((short)1);                // AudioFormat (1 = PCM)
        bw.Write((short)numChannels);      // NumChannels
        bw.Write(sampleRate);              // SampleRate
        bw.Write(byteRate);                // ByteRate
        bw.Write(blockAlign);              // BlockAlign
        bw.Write(bitsPerSample);           // BitsPerSample

        // data sub-chunk
        bw.Write(DATA);                    // SubChunk2ID
        bw.Write(dataSize);                // SubChunk2Size

        // sample data
        foreach (var s in samples)
            bw.Write(s);

        bw.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Builds a WAV with a LIST metadata chunk inserted between the <c>fmt</c>
    /// and <c>data</c> chunks, which exercises <c>WaveReader.SkipMetaData</c>.
    /// </summary>
    public static byte[] BuildWavWithListChunk(int sampleRate, short[] samples)
    {
        // The LIST chunk payload: "INFO" + arbitrary text
        byte[] listData = Encoding.ASCII.GetBytes("INFOtest");
        int listSize = listData.Length;

        int dataSize = samples.Length * sizeof(short);
        short blockAlign = 2;   // mono 16-bit
        int byteRate = sampleRate * 1 * 16 / 8;
        int riffPayloadSize = 4                          // WAVE
                            + 8 + 16                      // fmt  chunk
                            + 8 + listSize                // LIST chunk
                            + 8 + dataSize;               // data chunk

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write(RIFF);                    // ChunkID
        bw.Write(riffPayloadSize);         // ChunkSize (everything after this field)
        bw.Write(WAVE);                    // Format

        // fmt sub-chunk (standard 16-byte PCM)
        bw.Write(FMT);
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);               // mono
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write(blockAlign);
        bw.Write((short)16);

        // LIST chunk (before data — will be skipped by SkipMetaData)
        bw.Write(LIST);
        bw.Write(listSize);
        bw.Write(listData);

        // data sub-chunk
        bw.Write(DATA);
        bw.Write(dataSize);
        foreach (var s in samples)
            bw.Write(s);

        bw.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Creates a WAV with non-RIFF magic bytes at the start.
    /// </summary>
    public static byte[] BuildNonRiff(short[] samples)
    {
        var wav = BuildWav(16000, samples);
        // Overwrite the first 4 bytes (RIFF magic)
        wav[0] = (byte)'A';
        wav[1] = (byte)'B';
        wav[2] = (byte)'C';
        wav[3] = (byte)'D';
        return wav;
    }

    /// <summary>
    /// Writes WAV bytes to a temp file and returns the full path.
    /// </summary>
    public static string WriteTempWav(TempDir dir, string name, byte[] bytes)
    {
        var path = System.IO.Path.Combine(dir.Path, name);
        System.IO.File.WriteAllBytes(path, bytes);
        return path;
    }
}

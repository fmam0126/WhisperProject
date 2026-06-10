using System.Runtime.InteropServices;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SherpaOnnx;

namespace WhisperProject.Class;

public class VoiceEmphasisFilter
{
    /// <summary>
    /// Applies a voice emphasis band-pass filter (300Hz–3000Hz) to isolate speech frequencies
    /// and saves the result as a new WAV file.
    /// </summary>
    /// <param name="inputFilePath">Path to the input audio file (e.g., WAV format).</param>
    /// <param name="outputFilePath">Path where the processed audio file will be saved.</param>
    public void ApplyVoiceEmphasis(string inputFilePath, string outputFilePath)
    {
        // Read audio source
        using var reader = new AudioFileReader(inputFilePath);

        // Create band-pass filter chain: high-pass at 300Hz + low-pass at 3000Hz
        var highPassFilter = BiQuadFilter.HighPassFilter(reader.WaveFormat.SampleRate, 300, 1);
        var lowPassFilter = BiQuadFilter.LowPassFilter(reader.WaveFormat.SampleRate, 3000, 1);

        // Wrap the reader in a filtering sample provider that applies both filters
        var filteredProvider = new BandPassSampleProvider(reader, highPassFilter, lowPassFilter);

        // Write the filtered audio to the output file
        using var outputStream = File.Create(outputFilePath);
        WaveFileWriter.WriteWavFileToStream(outputStream, filteredProvider.ToWaveProvider16());
    }

    /// <summary>
    /// An <see cref="ISampleProvider"/> that applies two BiQuad filters in series to each sample.
    /// </summary>
    private sealed class BandPassSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly BiQuadFilter _highPass;
        private readonly BiQuadFilter _lowPass;

        public BandPassSampleProvider(ISampleProvider source, BiQuadFilter highPass, BiQuadFilter lowPass)
        {
            _source = source;
            _highPass = highPass;
            _lowPass = lowPass;
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(Span<float> buffer)
        {
            int samplesRead = _source.Read(buffer);

            for (int i = 0; i < samplesRead; i++)
            {
                float sample = buffer[i];
                sample = _highPass.Transform(sample);
                sample = _lowPass.Transform(sample);
                buffer[i] = sample;
            }

            return samplesRead;
        }
    }
    /// <summary>
    /// applies DPDFNet voice enhancement to the input audio file.
    /// </summary>
    /// <param name="inputFilePath">input file path</param>
    /// <param name="outputFilePath">output file path</param>
    /// <param name="modelPath">path to the voice enhancement onnx model</param>
    public void ApplyDpdfNetVoiceEnhancement(string inputFilePath, string outputFilePath, string modelPath)
    {
        var config = new OfflineSpeechDenoiserConfig();
        config.Model.Dpdfnet.Model = modelPath;
        config.Model.Debug = 1;
        config.Model.NumThreads = Environment.ProcessorCount;
        var sd = new OfflineSpeechDenoiser(config);

        var reader = new WaveReader(inputFilePath);
        var denoisedAudio = sd.Run(reader.Samples, reader.SampleRate);


        var success = denoisedAudio.SaveToWaveFile(outputFilePath);
        if (success)
        {
            Console.WriteLine($"saved denoised audio to {outputFilePath}");
        }
        else
        {
            Console.WriteLine($"failed to save denoised audio to {outputFilePath}");
        }
    }
}
public class WaveReader
{
    public WaveReader(string fileName)
    {
        if (!File.Exists(fileName))
        {
            throw new ApplicationException($"{fileName} does not exist!");
        }

        using var stream = File.Open(fileName, FileMode.Open);
        using var reader = new BinaryReader(stream);

        _header = ReadHeader(reader);

        if (!_header.Validate())
        {
            throw new ApplicationException($"Invalid wave file ${fileName}");
        }

        SkipMetaData(reader);

        // now read samples
        // _header.SubChunk2Size contains number of bytes in total.
        // we assume each sample is of type int16
        var buffer = reader.ReadBytes(_header.SubChunk2Size);
        var samples_int16 = new short[_header.SubChunk2Size / 2];
        Buffer.BlockCopy(buffer, 0, samples_int16, 0, buffer.Length);

        _samples = new float[samples_int16.Length];

        for (var i = 0; i < samples_int16.Length; ++i)
        {
            _samples[i] = samples_int16[i] / 32768.0F;
        }
    }

    private static WaveHeader ReadHeader(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(Marshal.SizeOf(typeof(WaveHeader)));

        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        WaveHeader header = (WaveHeader)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(WaveHeader))!;
        handle.Free();

        return header;
    }

    private void SkipMetaData(BinaryReader reader)
    {
        var bs = reader.BaseStream;

        var subChunk2ID = _header.SubChunk2ID;
        var subChunk2Size = _header.SubChunk2Size;

        while (bs.Position != bs.Length && subChunk2ID != 0x61746164)
        {
            bs.Seek(subChunk2Size, SeekOrigin.Current);
            subChunk2ID = reader.ReadInt32();
            subChunk2Size = reader.ReadInt32();
        }
        _header.SubChunk2ID = subChunk2ID;
        _header.SubChunk2Size = subChunk2Size;
    }

    private WaveHeader _header;

    // Samples are normalized to the range [-1, 1]
    private float[] _samples;

    public int SampleRate => _header.SampleRate;

    public float[] Samples => _samples;

    public static void Test(string fileName)
    {
        WaveReader reader = new WaveReader(fileName);
        Console.WriteLine($"samples length: {reader.Samples.Length}");
        Console.WriteLine($"samples rate: {reader.SampleRate}");
    }
}
public struct WaveHeader
{
    public int ChunkID;
    public int ChunkSize;
    public int Format;
    public int SubChunk1ID;
    public int SubChunk1Size;
    public short AudioFormat;
    public short NumChannels;
    public int SampleRate;
    public int ByteRate;
    public short BlockAlign;
    public short BitsPerSample;
    public int SubChunk2ID;
    public int SubChunk2Size;

    public bool Validate()
    {
        if (ChunkID != 0x46464952)
        {
            Console.WriteLine($"Invalid chunk ID: 0x{ChunkID:X}. Expect 0x46464952");
            return false;
        }

        //               E V A W
        if (Format != 0x45564157)
        {
            Console.WriteLine($"Invalid format: 0x{Format:X}. Expect 0x45564157");
            return false;
        }

        //                      t m f
        if (SubChunk1ID != 0x20746d66)
        {
            Console.WriteLine($"Invalid SubChunk1ID: 0x{SubChunk1ID:X}. Expect 0x20746d66");
            return false;
        }

        if (SubChunk1Size != 16)
        {
            Console.WriteLine($"Invalid SubChunk1Size: {SubChunk1Size}. Expect 16");
            return false;
        }

        if (AudioFormat != 1)
        {
            Console.WriteLine($"Invalid AudioFormat: {AudioFormat}. Expect 1");
            return false;
        }

        if (NumChannels != 1)
        {
            Console.WriteLine($"Invalid NumChannels: {NumChannels}. Expect 1");
            return false;
        }

        if (ByteRate != (SampleRate * NumChannels * BitsPerSample / 8))
        {
            Console.WriteLine($"Invalid byte rate: {ByteRate}.");
            return false;
        }

        if (BlockAlign != (NumChannels * BitsPerSample / 8))
        {
            Console.WriteLine($"Invalid block align: {ByteRate}.");
            return false;
        }

        if (BitsPerSample != 16)
        {  // we support only 16 bits per sample
            Console.WriteLine($"Invalid bits per sample: {BitsPerSample}. Expect 16");
            return false;
        }

        return true;
    }
}
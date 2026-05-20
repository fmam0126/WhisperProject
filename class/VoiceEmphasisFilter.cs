using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

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
}
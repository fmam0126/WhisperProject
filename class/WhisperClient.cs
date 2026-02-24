using System.ClientModel;
using System.Net.Http.Headers;
using System.Text.Json;
using OpenAI;
using OpenAI.Audio;

public class WhisperClient
{
    public async Task<string> TranscribeAsync(string model, string apiKey, string inputPath)
    {
        OpenAIClientOptions options = new()
        {
            Endpoint = new Uri("http://192.168.50.142:52625/v1")
        };

        OpenAIClient client = new(new ApiKeyCredential(apiKey), options);

        AudioClient audioClient = client.GetAudioClient(model);
        AudioTranscriptionOptions transcriptionOptions = new()
        {
            ResponseFormat = AudioTranscriptionFormat.Simple,
            TimestampGranularities = AudioTimestampGranularities.Word | AudioTimestampGranularities.Segment,
        };
        try
        {
            AudioTranscription transcription = await audioClient.TranscribeAudioAsync(inputPath, transcriptionOptions);
            Console.WriteLine(transcription);
            if (string.IsNullOrEmpty(transcription.Text))
            {
                return "Server returned 200 OK but empty text. Check if the model is loaded on the server.";
            }
            Console.WriteLine("Transcription:");
            Console.WriteLine($"{transcription.Text}");
            Console.WriteLine();
            Console.WriteLine($"Words:");
            foreach (TranscribedWord word in transcription.Words)
            {
                Console.WriteLine($"  {word.Word,15} : {word.StartTime.TotalMilliseconds,5:0} - {word.EndTime.TotalMilliseconds,5:0}");
            }

            Console.WriteLine();
            Console.WriteLine($"Segments:");
            foreach (TranscribedSegment segment in transcription.Segments)
            {
                Console.WriteLine($"  {segment.Text,90} : {segment.StartTime.TotalMilliseconds,5:0} - {segment.EndTime.TotalMilliseconds,5:0}");
            }
            return transcription.Text;
        }
        catch (Exception ex)
        {
            return $"API Error: {ex.Message}";
        }
        }
}
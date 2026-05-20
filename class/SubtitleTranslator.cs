using System.Text;
using System.Text.Json;
using SubtitlesParserV2;
using SubtitlesParserV2.Models;
using System.Collections.Concurrent;
using Polly;
using Polly.Retry;

namespace WhisperProject.Class;

public class SubtitleTranslator
{

    public string Url { get; set; } = string.Empty;
    public int Port { get; set; }
    public string GptPath { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public uint Concurrency { get; set; }

    public async Task TranslateSrtAsync(string srtFileName)
    {
        // This section reads the SRT file and translates the text using a translation API (e.g., Google Translate API).
        // You can implement the translation logic here using your preferred translation service.
        // For demonstration purposes, we will just print the original text and the target language.

        var semaphore = new SemaphoreSlim(4); // Limit Concurrency to 4


        using (FileStream fileStream = File.OpenRead(srtFileName))
        {
            // Try to parse with one specific parser using default configuration
            SubtitleParserResultModel? result = SubtitleParser.ParseStream(fileStream, Encoding.UTF8, SubtitleFormatType.SubRip);
            List<string> lines = new List<string>();
            List<int> startTimes = new List<int>();
            List<int> endTimes = new List<int>();

            if (result?.Subtitles != null)
            {
                // Create an instance of builder that exposes various extensions for adding resilience strategies
                ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
                    .AddRetry(new RetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        OnRetry = static args =>
                        {
                            Console.WriteLine($"Retry {args.AttemptNumber} triggered due to {args.Outcome.Exception?.Message}");
                            return default;
                        }

                    }) // Add retry 
                    .AddTimeout(TimeSpan.FromSeconds(30)) // Add 10 seconds timeout
                    .Build(); // Builds the resilience pipeline


                int Index = 0;
                int srtIndex = 1;
                string? translatedLine = string.Empty;
                lines = result.Subtitles.SelectMany(d => d.Lines).ToList();
                startTimes = result.Subtitles.Select(d => d.StartTime).ToList();
                endTimes = result.Subtitles.Select(d => d.EndTime).ToList();
                using var writer = new StreamWriter($"{Path.GetDirectoryName(Path.GetDirectoryName(srtFileName))}\\{Path.GetFileNameWithoutExtension(srtFileName)}.{TargetLanguage.ToUpper()}.srt");


                var translatedLines = new ConcurrentBag<(int Index, string Data)>();
                var translationTask = lines.Select(async (line, index) =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        string processedText = await pipeline.ExecuteAsync(async token => { return await SendToLLMAsync(PromptBuilder(line, WhisperClient.IdentifiedLanguage, TargetLanguage), UrlBuilder(Url)); });
                        translatedLines.Add((index, processedText));
                        Console.WriteLine(processedText.Trim().ReplaceLineEndings(string.Empty));
                        await Task.Delay(50);
                    }

                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(translationTask);

                foreach (var line in translatedLines.OrderBy(r => r.Index))
                {
                    await writer.WriteLineAsync(srtIndex.ToString());
                    await writer.WriteLineAsync($"{WhisperClient.FormatSrtTime(TimeSpan.FromMilliseconds(startTimes[Index]))} --> {WhisperClient.FormatSrtTime(TimeSpan.FromMilliseconds(endTimes[Index]))}");
                    await writer.WriteLineAsync(line.Data.Trim().ReplaceLineEndings(string.Empty));
                    await writer.WriteLineAsync("");
                    srtIndex++;
                    Index++;
                }


                // foreach (var line in lines)
                // {
                //     await writer.WriteLineAsync(srtIndex.ToString());
                //     await writer.WriteLineAsync($"{WhisperClient.FormatSrtTime(TimeSpan.FromMilliseconds(startTimes[Index]))} --> {WhisperClient.FormatSrtTime(TimeSpan.FromMilliseconds(endTimes[Index]))}");
                //     translatedLine = await SendToLLMAsync(PromptBuilder(line, WhisperClient.IdentifiedLanguage, TargetLanguage), UrlBuilder(Url));
                //     await writer.WriteLineAsync(translatedLine.Trim().ReplaceLineEndings(string.Empty));
                //     Console.WriteLine(translatedLine.Trim().ReplaceLineEndings(string.Empty));
                //     await writer.WriteLineAsync("");
                //     srtIndex++;
                //     Index++;
                // }
            }

        }
    }
    /// <summary>
    /// this async task translates the srt file to targeted language and then writes it to a srt file one directory up from the srt file
    /// </summary>
    /// <param name="srtFileName">srt file to translate</param>
    /// <returns></returns>
    public async Task TranslateSrtParalellTask(string srtFileName)
    {
        using (FileStream fileStream = File.OpenRead(srtFileName))
        {
            // Try to parse with one specific parser using default configuration
            SubtitleParserResultModel? result = SubtitleParser.ParseStream(fileStream, Encoding.UTF8, SubtitleFormatType.SubRip);
            List<string> lines = new List<string>();
            List<int> startTimes = new List<int>();
            List<int> endTimes = new List<int>();

            if (result?.Subtitles != null)
            {

                int Index = 0;
                int srtIndex = 1;
                string? translatedLine = string.Empty;
                lines = result.Subtitles.SelectMany(d => d.Lines).ToList();
                startTimes = result.Subtitles.Select(d => d.StartTime).ToList();
                endTimes = result.Subtitles.Select(d => d.EndTime).ToList();
                using var writer = new StreamWriter($"{Path.GetDirectoryName(Path.GetDirectoryName(srtFileName))}\\{Path.GetFileNameWithoutExtension(srtFileName)}.{TargetLanguage.ToUpper()}.srt");


                // var translatedLines = new ConcurrentBag<(int Index, string Data)>();
                var translatedLines = new List<string>();

                var options = new ParallelOptions { MaxDegreeOfParallelism = 4 }; // limit concurrency to 4 
                await Parallel.ForEachAsync(lines, options, async (line, CancellationToken) =>
                {
                    string processedText;
                    try
                    {
                        processedText = await SendToLLMAsync(PromptBuilder(line, WhisperClient.IdentifiedLanguage, TargetLanguage), UrlBuilder(Url));

                    }
                    catch (System.Exception)
                    {

                        throw;
                    }
                    translatedLines.Add(processedText);
                    Console.WriteLine(processedText.Trim().ReplaceLineEndings(string.Empty));
                    await Task.Delay(50);
                });


                foreach (var line in translatedLines)
                {
                    await writer.WriteLineAsync(srtIndex.ToString());
                    await writer.WriteLineAsync($"{WhisperClient.FormatSrtTime(TimeSpan.FromMilliseconds(startTimes[Index]))} --> {WhisperClient.FormatSrtTime(TimeSpan.FromMilliseconds(endTimes[Index]))}");
                    await writer.WriteLineAsync(line.Trim().ReplaceLineEndings(string.Empty));
                    await writer.WriteLineAsync("");
                    srtIndex++;
                    Index++;
                }


                // foreach (var line in lines)
                // {
                //     await writer.WriteLineAsync(srtIndex.ToString());
                //     await writer.WriteLineAsync($"{WhisperClient.FormatSrtTime(TimeSpan.FromMilliseconds(startTimes[Index]))} --> {WhisperClient.FormatSrtTime(TimeSpan.FromMilliseconds(endTimes[Index]))}");
                //     translatedLine = await SendToLLMAsync(PromptBuilder(line, WhisperClient.IdentifiedLanguage, TargetLanguage), UrlBuilder(Url));
                //     await writer.WriteLineAsync(translatedLine.Trim().ReplaceLineEndings(string.Empty));
                //     Console.WriteLine(translatedLine.Trim().ReplaceLineEndings(string.Empty));
                //     await writer.WriteLineAsync("");
                //     srtIndex++;
                //     Index++;
                // }
            }

        }



        // using var reader = new StreamReader(srtFileName);
        // string line;
        // while ((line = await reader.ReadLineAsync()) != null)
        // {

        //     // Here you would call your translation API to translate 'line' to 'TargetLanguage'
        //     using (FileStream fileStream = File.OpenRead(srtFileName))
        //     {
        //         SubtitleParserResultModel? result = SubtitlesParserV2.SubtitleParser.ParseStream(fileStream, Encoding.UTF8);
        //         var subs = result.Subtitles; 
        //     }

        //     Console.WriteLine($"Original: {line} | Translated to {TargetLanguage}: [Translated Text]");
        // }
    }

    private string RemoveReasoning(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        try
        {
            return System.Text.RegularExpressions.Regex.Replace(text, @"<think>.*?</think>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing think tags: {ex.Message}");
            return text;
        }
    }
    private string UrlBuilder(string baseurl)
    {
        if (baseurl == null)
            throw new InvalidOperationException("Translate config not set.");

        // Build full URL with endpoint, optional port, and path
        string baseEndpoint = Url?.Trim() ?? "http://127.0.0.1";
        Uri? baseUri;
        if (!Uri.TryCreate(baseEndpoint, UriKind.Absolute, out baseUri))
        {
            // try adding scheme
            if (Uri.TryCreate("http://" + baseEndpoint, UriKind.Absolute, out baseUri) == false)
                throw new InvalidOperationException("Invalid endpoint URL.");
        }

        var builder = new UriBuilder(baseUri);
        if (Port > 0)
            builder.Port = Port;
        builder.Path = (GptPath ?? builder.Path ?? string.Empty).TrimStart('/');
        var url = builder.Uri.ToString();
        return url;
    }
    private string PromptBuilder(string Prompt, string language, string TargetLanguage)
    {
        return $"Please Translate the following text from {language} to {TargetLanguage} without adding comments. just output translated text: \n{Prompt}";
    }
    private async Task<string> SendToLLMAsync(string Prompt, string url)
    {
        HttpClient httpClient = new HttpClient();

        var messages = new List<object>();
        messages.Add(new { role = "user", content = Prompt });
        var payload = new Dictionary<string, object>
        {
            { "model", string.IsNullOrWhiteSpace(Model) ? "" : Model},
            { "messages", messages}

        };
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(url, content);
        if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
        {
            Console.WriteLine($"{response.Headers} {response.Content} {response}");

        }
        response.EnsureSuccessStatusCode();

        var responseText = await response.Content.ReadAsStringAsync();

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var rootElement = document.RootElement;
            if (rootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentElement))
                {
                    var result = contentElement.GetString();
                    return RemoveReasoning(result ?? string.Empty);
                }
                if (first.TryGetProperty("text", out var textElement))
                {
                    var result = textElement.GetString();
                    return RemoveReasoning(result ?? string.Empty);
                }
            }

            if (rootElement.TryGetProperty("text", out var text))
            {
                var result = text.GetString();
                return RemoveReasoning(result ?? string.Empty);
            }
            if (rootElement.TryGetProperty("result", out var resultElement))
            {
                var result = resultElement.GetString();
                return RemoveReasoning(result ?? string.Empty);
            }
            Console.WriteLine($"WARNING: Could not parse Response. response length {responseText.Length}");
            return responseText;
        }
        catch (Exception parseEx)
        {
            Console.WriteLine($"Error parsing API response: {parseEx.Message}. Response length: {responseText.Length}");
            return responseText;
        }

    }
}
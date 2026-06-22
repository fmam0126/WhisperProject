using System.ClientModel;
using System.Text;
using SubtitlesParserV2;
using SubtitlesParserV2.Models;
using System.Collections.Concurrent;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using Polly;
using Polly.Retry;
using Microsoft.Extensions.AI;

namespace WhisperProject.Class;

public class SubtitleTranslator
{

    public string Url { get; set; } = string.Empty;
    public int Port { get; set; }
    public string GptPath { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public uint Concurrency { get; set; } = 4;
    public string SystemPrompt { get; set; } = "You are a helpful assistant for translating video subtitles. You receive text in the format of a subtitle file and you translate it to the target language without adding any comments or explanations, just output the translated text. Always keep the formatting of the original text.";

    private AIAgent? _agent;

    /// <summary>
    /// Lazily creates or returns a configured <see cref="AIAgent"/> backed by an
    /// OpenAI-compatible endpoint (works with local LLMs like Ollama, LM Studio, llama.cpp, etc.).
    /// </summary>
    private AIAgent GetOrCreateAgent()
    {
        if (_agent is not null)
            return _agent;

        var endpointUri = BuildEndpointUri();
        var clientOptions = new OpenAIClientOptions { Endpoint = endpointUri };

        var openAIClient = new OpenAIClient(
            new ApiKeyCredential(string.IsNullOrWhiteSpace(ApiKey) ? "not-needed" : ApiKey),
            clientOptions);

        var chatClient = openAIClient.GetChatClient(
            string.IsNullOrWhiteSpace(Model) ? string.Empty : Model);

        _agent = chatClient.AsAIAgent(instructions: SystemPrompt);
        return _agent;
    }

    /// <summary>
    /// this async task translates the srt file to targeted language and then writes it to a srt file one directory up from the srt file, it uses a semaphore to limit concurrency to 4 and Polly to add retry and timeout policies to the translation API calls
    /// </summary>
    /// <param name="srtFileName">input srt file name to translate</param>
    /// <param name="outputPath">output path for the translated srt file</param>
    /// <returns></returns>
    public async Task TranslateSrtAsync(string srtFileName, string outputPath)
    {
        // This section reads the SRT file and translates the text using a translation API (e.g., Google Translate API).
        // You can implement the translation logic here using your preferred translation service.
        // For demonstration purposes, we will just print the original text and the target language.

        var semaphore = new SemaphoreSlim((int)Concurrency); // Limit Concurrency to 4


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
                    .AddTimeout(TimeSpan.FromMinutes(2)) // Add 2 minute timeout
                    .Build(); // Builds the resilience pipeline


                int Index = 0;
                int srtIndex = 1;
                string? translatedLine = string.Empty;
                lines = result.Subtitles.SelectMany(d => d.Lines).ToList();
                startTimes = result.Subtitles.Select(d => d.StartTime).ToList();
                endTimes = result.Subtitles.Select(d => d.EndTime).ToList();
                using var writer = new StreamWriter($"{outputPath}\\{Path.GetFileNameWithoutExtension(srtFileName)}.{TargetLanguage.ToUpper()}.srt");


                var translatedLines = new ConcurrentBag<(int Index, string Data)>();
                var translationTask = lines.Select(async (line, index) =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        string processedText = await pipeline.ExecuteAsync(async token => { return await SendToLLMAsync(PromptBuilder(line, WhisperClient.IdentifiedLanguage, TargetLanguage), string.Empty, token); });
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
    /// <param name="outputPath">output path for the translated srt file</param>
    /// <returns></returns>
    public async Task TranslateSrtParalellTask(string srtFileName, string outputPath)
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
                using var writer = new StreamWriter($"{outputPath}\\{Path.GetFileNameWithoutExtension(srtFileName)}.{TargetLanguage.ToUpper()}.srt");


                // var translatedLines = new ConcurrentBag<(int Index, string Data)>();
                var translatedLines = new List<string>();

                var options = new ParallelOptions { MaxDegreeOfParallelism = 4 }; // limit concurrency to 4 
                await Parallel.ForEachAsync(lines, options, async (line, cancellationToken) =>
                {
                    string processedText;
                    try
                    {
                        processedText = await SendToLLMAsync(PromptBuilder(line, WhisperClient.IdentifiedLanguage, TargetLanguage), string.Empty, cancellationToken);

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
    /// <summary>
    /// Builds the OpenAI-compatible base endpoint URI from the configured properties.
    /// The <see cref="GptPath"/> is used as the API base path (e.g. "/v1").
    /// If <see cref="GptPath"/> includes a "/chat/completions" suffix it is stripped since
    /// the OpenAI SDK appends it automatically.
    /// </summary>
    private Uri BuildEndpointUri()
    {
        string baseEndpoint = (Url ?? "http://127.0.0.1").Trim();
        if (!Uri.TryCreate(baseEndpoint, UriKind.Absolute, out var baseUri))
        {
            if (!Uri.TryCreate("http://" + baseEndpoint, UriKind.Absolute, out baseUri))
                throw new InvalidOperationException("Invalid endpoint URL.");
        }

        var builder = new UriBuilder(baseUri);
        if (Port > 0)
            builder.Port = Port;

        // Use GptPath as the API base path. Strip /chat/completions suffix
        // because the OpenAI SDK appends it automatically.
        var path = (GptPath ?? string.Empty).Trim('/');
        if (path.EndsWith("chat/completions", StringComparison.OrdinalIgnoreCase))
            path = path[..^"chat/completions".Length].Trim('/');

        if (!string.IsNullOrWhiteSpace(path))
            builder.Path = path;

        return builder.Uri;
    }
    private string PromptBuilder(string Prompt, string language, string TargetLanguage)
    {
        return $"Please Translate the following text from {language} to {TargetLanguage} without adding comments. just output translated text: \n{Prompt}";
    }
    private async Task<string> SendToLLMAsync(string Prompt, string url, CancellationToken cancellationToken = default)
    {
        var agent = GetOrCreateAgent();

        try
        {
            // RunAsync(string) sends the prompt as a user message and returns AgentResponse.
            AgentResponse response = await agent.RunAsync(Prompt, cancellationToken: cancellationToken);
            string result = response.ToString() ?? string.Empty;
            return RemoveReasoning(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Agent call failed: {ex.Message}");
            throw;
        }
    }
}
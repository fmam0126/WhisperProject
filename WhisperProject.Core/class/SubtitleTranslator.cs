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
    /// <summary>
    /// url of the OpenAI-compatible endpoint (e.g. Ollama, LM Studio, llama.cpp, etc.) to use for translation.
    /// </summary>
    public string Url { get; set; } = string.Empty;
    /// <summary>
    /// Port of the OpenAI-compatible endpoint (e.g. Ollama, LM Studio, llama.cpp, etc.) to use for translation.
    /// </summary>
    public int Port { get; set; }
    /// <summary>
    /// Path of the OpenAI-compatible endpoint (e.g. Ollama, LM Studio, llama.cpp, etc.) to use for translation.
    /// This is the base path for the API (e.g. "/v1/chat/completions").
    /// </summary>
    public string GptPath { get; set; } = string.Empty;
    /// <summary>
    /// Model name to use for translation (e.g. "gpt-4o", "llama2-13b-chat", etc.).
    /// </summary>
    public string Model { get; set; } = string.Empty;
    /// <summary>
    /// Target language code for translation (e.g. "es" for Spanish, "fr" for French, etc.).
    /// </summary>
    public string TargetLanguage { get; set; } = string.Empty;
    /// <summary>
    /// API key for the OpenAI-compatible endpoint, if required. Some local LLMs may not require an API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>
    /// Maximum number of concurrent translation requests to the LLM. This limits how many Requests are sent in parallel to avoid overwhelming the endpoint or hitting rate limits. Default is 4.
    /// </summary>
    public uint Concurrency { get; set; } = 4;
    /// <summary>
    /// Number of subtitle entries to send to the LLM in a single batch for context-aware translation.
    /// </summary>
    public uint ContextSize { get; set; } = 5;
    /// <summary>
    /// System prompt that instructs the LLM on how to perform subtitle translation.
    /// </summary>
    public string SystemPrompt { get; set; } = "You are a helpful assistant for translating video subtitles. You receive text in the format of a subtitle file and you translate it to the target language without adding any comments or explanations, just output the translated text. Always keep the formatting of the original text.";
    /// <summary>
    /// Lazily initialized <see cref="AIAgent"/> instance used to send prompts to the LLM. It is created on first use and reused for subsequent translation requests.
    /// </summary>
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
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = endpointUri,
            NetworkTimeout = TimeSpan.FromMinutes(6) // Override the default 100s timeout
        };

        var openAIClient = new OpenAIClient(
            new ApiKeyCredential(string.IsNullOrWhiteSpace(ApiKey) ? "not-needed" : ApiKey),
            clientOptions);

        var chatClient = openAIClient.GetChatClient(
            string.IsNullOrWhiteSpace(Model) ? string.Empty : Model);

        _agent = chatClient.AsAIAgent(instructions: SystemPrompt);
        return _agent;
    }

    /// <summary>
    /// this async task translates the srt file to targeted language and then writes it to a srt file one directory up from the srt file, 
    /// it uses a semaphore to limit concurrency to 4 and Polly to add retry and timeout policies to the translation API calls
    /// </summary>
    /// <param name="srtFileName">input srt file name to translate</param>
    /// <param name="outputPath">output path for the translated srt file</param>
    /// <returns></returns>
    public async Task TranslateSrtAsync(string srtFileName, string outputPath)
    {
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
                    .AddTimeout(TimeSpan.FromMinutes(5)) // Add 5 minute timeout
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
                        string processedText = await pipeline.ExecuteAsync(async token => { return await SendToLLMAsync(PromptBuilder(line, WhisperClient.IdentifiedLanguage, TargetLanguage), token); });
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
    /// Translates an SRT file by sending batches of <see cref="ContextSize"/> subtitle entries
    /// to the LLM at once, providing surrounding context for better translation quality.
    /// Each batch is sent as numbered lines; the LLM is expected to return translated lines
    /// with the same numbering for reliable parsing.
    /// </summary>
    /// <param name="srtFileName">Input SRT file path.</param>
    /// <param name="outputPath">Directory where the translated SRT file will be written.</param>
    public async Task TranslateSrtWithContextAsync(string srtFileName, string outputPath)
    {
        var semaphore = new SemaphoreSlim((int)Concurrency);

        using FileStream fileStream = File.OpenRead(srtFileName);
        SubtitleParserResultModel? result = SubtitleParser.ParseStream(fileStream, Encoding.UTF8, SubtitleFormatType.SubRip);

        if (result?.Subtitles is null || result.Subtitles.Count == 0)
        {
            Console.WriteLine("No subtitles found to translate.");
            return;
        }

        var subtitleItems = result.Subtitles; // preserves StartTime, EndTime, Lines per entry
        int batchSize = (int)ContextSize;

        // Build batches: each batch is a contiguous range of subtitle entries
        var batches = new List<List<SubtitleModel>>();
        for (int i = 0; i < subtitleItems.Count; i += batchSize)
        {
            batches.Add(subtitleItems.Skip(i).Take(batchSize).ToList());
        }

        // Resilience pipeline (retry + timeout)
        ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                OnRetry = static args =>
                {
                    Console.WriteLine($"Retry {args.AttemptNumber} triggered due to {args.Outcome.Exception?.Message}");
                    return default;
                }
            })
            .AddTimeout(TimeSpan.FromMinutes(5))
            .Build();

        // ConcurrentBag to collect results in order
        var translatedBatches = new ConcurrentBag<(int BatchIndex, List<string> TranslatedLines)>();

        var batchTasks = batches.Select(async (batch, batchIndex) =>
        {
            await semaphore.WaitAsync();
            try
            {
                // Collect all lines from this batch with global numbering
                var allLines = batch.SelectMany(item => item.Lines).ToList();

                string prompt = BuildBatchPrompt(allLines, WhisperClient.IdentifiedLanguage, TargetLanguage);
                string responseText = await pipeline.ExecuteAsync(
                    async token => await SendToLLMAsync(prompt, token));

                // Parse the numbered response back into individual lines
                var translatedLines = ParseNumberedResponse(responseText, allLines.Count);
                translatedBatches.Add((batchIndex, translatedLines));

                foreach (var line in translatedLines)
                    Console.WriteLine(line.Trim().ReplaceLineEndings(string.Empty));
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(batchTasks);

        // Reconstruct output: walk batches in order, then items in order, then lines in order
        using var writer = new StreamWriter(
            $"{outputPath}\\{Path.GetFileNameWithoutExtension(srtFileName)}.{TargetLanguage.ToUpper()}.srt");

        int srtIndex = 1;
        int globalLineIndex = 0;
        // Produces blank subtitle entries if the LLM returned fewer lines than expected. - needs fixing
        foreach (var batch in batches)
        {
            foreach (var item in batch)
            {
                await writer.WriteLineAsync(srtIndex.ToString());
                await writer.WriteLineAsync(
                    $"{WhisperClient.FormatSrtTime(TimeSpan.FromMilliseconds(item.StartTime))} --> {WhisperClient.FormatSrtTime(TimeSpan.FromMilliseconds(item.EndTime))}");

                // Write all translated lines for this subtitle entry
                for (int i = 0; i < item.Lines.Count; i++)
                {
                    // Find the translated text from the collected results
                    string translatedText = GetTranslatedLine(translatedBatches, globalLineIndex);
                    await writer.WriteLineAsync(translatedText.Trim().ReplaceLineEndings(string.Empty));
                    globalLineIndex++;
                }

                await writer.WriteLineAsync("");
                srtIndex++;
            }
        }
    }

    /// <summary>
    /// Retrieves a single translated line from the collected batch results by its global index.
    /// </summary>
    /// <param name="translatedBatches">The collection of translated batches.</param>
    /// <param name="globalLineIndex">The global index of the line to retrieve.</param>
    /// <returns>The translated line, or an empty string if not found.</returns>
    private static string GetTranslatedLine(
        ConcurrentBag<(int BatchIndex, List<string> TranslatedLines)> translatedBatches,
        int globalLineIndex)
    {
        // Walk through batches in order to find the line
        int offset = 0;
        foreach (var batch in translatedBatches.OrderBy(b => b.BatchIndex))
        {
            if (globalLineIndex < offset + batch.TranslatedLines.Count)
                return batch.TranslatedLines[globalLineIndex - offset];
            offset += batch.TranslatedLines.Count;
        }
        return string.Empty; // fallback
    }
    /// <summary>
    /// Builds a batch translation prompt with numbered lines so the LLM can return
    /// translations in a corresponding numbered format for simple parsing.
    /// </summary>
    /// <param name="lines">The list of subtitle lines to translate.</param>
    /// <param name="sourceLanguage">The source language of the subtitle lines.</param>
    /// <param name="targetLanguage">The target language for the translation.</param>
    /// <returns>The constructed translation prompt.</returns>
    private string BuildBatchPrompt(List<string> lines, string sourceLanguage, string targetLanguage)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Translate the following {lines.Count} subtitle lines from {sourceLanguage} to {targetLanguage}.");
        sb.AppendLine("Translate each line individually while preserving the meaning in context.");
        sb.AppendLine("Return ONLY the translated lines prefixed with their line number in the format '[N] translated text'.");
        sb.AppendLine("Do not add any comments, explanations, or markdown formatting.");
        sb.AppendLine();

        for (int i = 0; i < lines.Count; i++)
        {
            sb.AppendLine($"[{i + 1}] {lines[i]}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses the LLM response that contains numbered translated lines in the format
    /// "[N] translated text" and returns them in order, one per original line.
    /// </summary>
    /// <param name="response">The LLM response to parse.</param>
    /// <param name="expectedCount">The expected number of translated lines.</param>
    /// <returns>A list of translated lines in the correct order.</returns>
    internal static List<string> ParseNumberedResponse(string response, int expectedCount)
    {
        var translated = new List<string>();

        // Split into lines and try to extract [N] prefixed entries
        var responseLines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(l => l.Trim())
                                     .ToList();

        foreach (var line in responseLines)
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, @"^\[(\d+)\]\s*(.*)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
            {
                // Ensure we don't go out of bounds; pad with empty if LLM returns extra
                while (translated.Count < num - 1)
                    translated.Add(string.Empty);

                if (translated.Count < num)
                    translated.Add(match.Groups[2].Value.Trim());
                else
                    translated[num - 1] = match.Groups[2].Value.Trim(); // overwrite duplicates
            }
        }

        // Pad or trim to expected count
        while (translated.Count < expectedCount)
            translated.Add(string.Empty);

        if (translated.Count > expectedCount)
            translated = translated.Take(expectedCount).ToList();

        return translated;
    }
    /// <summary>
    /// uses regex to remove <think></think> tags from input text
    /// </summary>
    /// <param name="text">string to remove reasoning tags from</param>
    /// <returns>string without reasoning tags</returns>
    internal string RemoveReasoning(string text)
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
    internal Uri BuildEndpointUri()
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
    internal string PromptBuilder(string Prompt, string language, string TargetLanguage)
    {
        return $"Please Translate the following text from {language} to {TargetLanguage} without adding comments. just output translated text: \n{Prompt}";
    }
    private async Task<string> SendToLLMAsync(string Prompt, CancellationToken cancellationToken = default)
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
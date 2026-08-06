using WhisperProject.Class;

namespace WhisperProject.Tests.Core;

/// <summary>
/// Tests for internal pure-logic members of <see cref="SubtitleTranslator"/>:
/// <c>ParseNumberedResponse</c>, <c>BuildEndpointUri</c>,
/// <c>RemoveReasoning</c>, and <c>PromptBuilder</c>.
/// </summary>
public class SubtitleTranslatorTests
{
    // ── ParseNumberedResponse ────────────────────────────────────────────

    [Fact]
    public void ParseNumberedResponseWellFormedReturnsLinesInOrder()
    {
        var result = SubtitleTranslator.ParseNumberedResponse(
            "[1] hello\n[2] world", 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("hello", result[0]);
        Assert.Equal("world", result[1]);
    }

    [Fact]
    public void ParseNumberedResponseWithGapsPadsWithEmptyStrings()
    {
        var result = SubtitleTranslator.ParseNumberedResponse(
            "[1] a\n[3] c", 3);

        Assert.Equal(3, result.Count);
        Assert.Equal("a", result[0]);
        Assert.Equal("", result[1]);
        Assert.Equal("c", result[2]);
    }

    [Fact]
    public void ParseNumberedResponseMoreLinesThanExpectedTrimsTail()
    {
        var result = SubtitleTranslator.ParseNumberedResponse(
            "[1] a\n[2] b\n[3] c", 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("a", result[0]);
        Assert.Equal("b", result[1]);
    }

    [Fact]
    public void ParseNumberedResponseFewerLinesThanExpectedPadsTail()
    {
        var result = SubtitleTranslator.ParseNumberedResponse(
            "[1] a\n[2] b", 4);

        Assert.Equal(4, result.Count);
        Assert.Equal("a", result[0]);
        Assert.Equal("b", result[1]);
        Assert.Equal("", result[2]);
        Assert.Equal("", result[3]);
    }

    [Fact]
    public void ParseNumberedResponseNoNumberingAllEmpty()
    {
        var result = SubtitleTranslator.ParseNumberedResponse(
            "hello\nworld", 2);

        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal("", s));
    }

    [Fact]
    public void ParseNumberedResponseDuplicateNumbersLastWins()
    {
        var result = SubtitleTranslator.ParseNumberedResponse(
            "[1] first\n[1] second", 1);

        Assert.Single(result);
        Assert.Equal("second", result[0]);
    }

    [Fact]
    public void ParseNumberedResponseEmptyResponseAllEmpty()
    {
        var result = SubtitleTranslator.ParseNumberedResponse("", 3);

        Assert.Equal(3, result.Count);
        Assert.All(result, s => Assert.Equal("", s));
    }

    [Fact]
    public void ParseNumberedResponseContinuationLinesIgnored()
    {
        var result = SubtitleTranslator.ParseNumberedResponse(
            "[1] a\ncontinuation\n[2] b", 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("a", result[0]);
        Assert.Equal("b", result[1]);
    }

    [Fact]
    public void ParseNumberedResponseWhitespacePaddedNumbersTrimmed()
    {
        var result = SubtitleTranslator.ParseNumberedResponse(
            "  [1]  hello  ", 1);

        Assert.Single(result);
        Assert.Equal("hello", result[0]);
    }

    [Fact]
    public void ParseNumberedResponseExtraSpacingBeforeBracketIgnored()
    {
        // The regex requires ^\[ — lines with leading whitespace before [ don't match
        var result = SubtitleTranslator.ParseNumberedResponse(
            "   [1] hello", 1);

        Assert.Single(result);
        Assert.Equal("hello", result[0]);
    }

    // ── BuildEndpointUri ─────────────────────────────────────────────────

    [Fact]
    public void BuildEndpointUriUrlPortPathComposes()
    {
        var translator = new SubtitleTranslator
        {
            Url = "http://127.0.0.1",
            Port = 1234,
            GptPath = "/v1"
        };

        var uri = translator.BuildEndpointUri();

        Assert.Equal("http://127.0.0.1:1234/v1", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildEndpointUriChatCompletionsSuffixStripped()
    {
        var translator = new SubtitleTranslator
        {
            Url = "http://127.0.0.1",
            Port = 1234,
            GptPath = "/v1/chat/completions"
        };

        var uri = translator.BuildEndpointUri();

        Assert.Equal("http://127.0.0.1:1234/v1", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildEndpointUriChatCompletionsSuffixOnlySuffixReturnsRoot()
    {
        var translator = new SubtitleTranslator
        {
            Url = "http://127.0.0.1",
            Port = 1234,
            GptPath = "/chat/completions"
        };

        var uri = translator.BuildEndpointUri();

        Assert.Equal("http://127.0.0.1:1234/", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildEndpointUriChatCompletionsSuffixCaseInsensitive()
    {
        var translator = new SubtitleTranslator
        {
            Url = "http://127.0.0.1",
            Port = 1234,
            GptPath = "/v1/CHAT/COMPLETIONS"
        };

        var uri = translator.BuildEndpointUri();

        Assert.Equal("http://127.0.0.1:1234/v1", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildEndpointUriNoSchemePrependsHttp()
    {
        var translator = new SubtitleTranslator
        {
            Url = "127.0.0.1",
            Port = 1234,
            GptPath = "/v1"
        };

        var uri = translator.BuildEndpointUri();

        Assert.Equal("http://127.0.0.1:1234/v1", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildEndpointUriEmptyGptPathNoPathComponent()
    {
        var translator = new SubtitleTranslator
        {
            Url = "http://127.0.0.1",
            Port = 1234,
            GptPath = ""
        };

        var uri = translator.BuildEndpointUri();

        Assert.Equal("http://127.0.0.1:1234/", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildEndpointUriPortZeroKeepsUrlPort()
    {
        var translator = new SubtitleTranslator
        {
            Url = "http://localhost:8080",
            Port = 0,
            GptPath = ""
        };

        var uri = translator.BuildEndpointUri();

        Assert.Equal("http://localhost:8080/", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildEndpointUriTrailingSlashesTrimmed()
    {
        var translator = new SubtitleTranslator
        {
            Url = "http://127.0.0.1",
            Port = 1234,
            GptPath = "/v1//"
        };

        var uri = translator.BuildEndpointUri();

        Assert.Equal("http://127.0.0.1:1234/v1", uri.AbsoluteUri);
    }

    [Fact]
    public void BuildEndpointUriInvalidUrlThrowsInvalidOperationException()
    {
        var translator = new SubtitleTranslator
        {
            Url = ":::",
            Port = 1234,
            GptPath = "/v1"
        };

        Assert.Throws<InvalidOperationException>(() => translator.BuildEndpointUri());
    }

    [Fact]
    public void BuildEndpointUriNullUrlFallsBackToDefault()
    {
        // The code uses (Url ?? "http://127.0.0.1"), so null falls back but
        // empty string does not (it passes "" to Uri.TryCreate which fails).
        var translator = new SubtitleTranslator
        {
            Url = null!,
            Port = 1234,
            GptPath = "/v1"
        };

        var uri = translator.BuildEndpointUri();

        Assert.Equal("http://127.0.0.1:1234/v1", uri.AbsoluteUri);
    }

    // ── RemoveReasoning ──────────────────────────────────────────────────

    [Fact]
    public void RemoveReasoningSimpleTagStripped()
    {
        var translator = new SubtitleTranslator();
        var result = translator.RemoveReasoning(
            "<think>secret reasoning</think>hello world");

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void RemoveReasoningMultilineTagStripped()
    {
        var translator = new SubtitleTranslator();
        var input = "<think>\nline one\nline two\n</think>\nactual text";

        var result = translator.RemoveReasoning(input);

        Assert.Equal("\nactual text", result);
    }

    [Fact]
    public void RemoveReasoningMultipleTagsAllStripped()
    {
        var translator = new SubtitleTranslator();
        var result = translator.RemoveReasoning(
            "<think>one</think>prefix<think>two</think>suffix");

        Assert.Equal("prefixsuffix", result);
    }

    [Fact]
    public void RemoveReasoningNoTagsUnchanged()
    {
        var translator = new SubtitleTranslator();
        var input = "just plain text";

        var result = translator.RemoveReasoning(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void RemoveReasoningCaseInsensitiveStripped()
    {
        var translator = new SubtitleTranslator();
        var result = translator.RemoveReasoning(
            "<THINK>case test</THINK>visible");

        Assert.Equal("visible", result);
    }

    [Fact]
    public void RemoveReasoningNullReturnsNull()
    {
        var translator = new SubtitleTranslator();
        var result = translator.RemoveReasoning(null!);

        Assert.Null(result);
    }

    [Fact]
    public void RemoveReasoningEmptyReturnsEmpty()
    {
        var translator = new SubtitleTranslator();
        var result = translator.RemoveReasoning("");

        Assert.Equal("", result);
    }

    // ── PromptBuilder ────────────────────────────────────────────────────

    [Fact]
    public void PromptBuilderExactFormat()
    {
        var translator = new SubtitleTranslator();
        var result = translator.PromptBuilder("Hello world", "English", "Norwegian");

        Assert.Equal(
            "Please Translate the following text from English to Norwegian without adding comments. just output translated text: \nHello world",
            result);
    }

    [Fact]
    public void PromptBuilderIncludesNewlineSeparator()
    {
        var translator = new SubtitleTranslator();
        var result = translator.PromptBuilder("test prompt", "en", "fr");

        Assert.Contains("\ntest prompt", result);
        Assert.StartsWith("Please Translate the following text from en to fr", result);
    }
}

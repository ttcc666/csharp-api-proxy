using Microsoft.Extensions.Options;

namespace OpenAI_Compatible_API_Proxy_for_Z.Services;

public class OpenAIResponseBuilder : IOpenAIResponseBuilder
{
    private readonly ProxySettings _settings;

    public OpenAIResponseBuilder(IOptions<ProxySettings> settings)
    {
        _settings = settings.Value;
    }

    public OpenAIResponse BuildStreamChunk(string? content = null, string? reasoningContent = null, bool isFirst = false, bool isLast = false)
    {
        var chatId = $"chatcmpl-{DateTimeOffset.Now.ToUnixTimeSeconds()}";
        var created = DateTimeOffset.Now.ToUnixTimeSeconds();

        if (isFirst)
        {
            return new OpenAIResponse(
                Id: chatId,
                Object: "chat.completion.chunk",
                Created: created,
                Model: ModelNames.Default,
                Choices: new List<Choice> { new Choice(0, null, new Delta("assistant", null, null), null) },
                Usage: null
            );
        }

        if (isLast)
        {
            return new OpenAIResponse(
                Id: chatId,
                Object: "chat.completion.chunk",
                Created: created,
                Model: ModelNames.Default,
                Choices: new List<Choice> { new Choice(0, null, new Delta(null, null, null), "stop") },
                Usage: null
            );
        }

        return new OpenAIResponse(
            Id: chatId,
            Object: "chat.completion.chunk",
            Created: created,
            Model: _settings.ModelName,
            Choices: new List<Choice> { new Choice(0, null, new Delta(null, content, reasoningContent), null) },
            Usage: null
        );
    }

    public OpenAIResponse BuildFinalResponse(string content, string? reasoningContent = null, Usage? usage = null)
    {
        return new OpenAIResponse(
            Id: $"chatcmpl-{DateTimeOffset.Now.ToUnixTimeSeconds()}",
            Object: "chat.completion",
            Created: DateTimeOffset.Now.ToUnixTimeSeconds(),
            Model: _settings.ModelName,
            Choices: new List<Choice> { new Choice(0, new Message("assistant", content, reasoningContent), null, "stop") },
            Usage: usage ?? new Usage(0, 0, 0)
        );
    }
}

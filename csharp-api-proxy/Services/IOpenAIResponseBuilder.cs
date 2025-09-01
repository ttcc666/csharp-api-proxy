namespace OpenAI_Compatible_API_Proxy_for_Z.Services;

public interface IOpenAIResponseBuilder
{
    OpenAIResponse BuildStreamChunk(string? content = null, string? reasoningContent = null, bool isFirst = false, bool isLast = false);
    OpenAIResponse BuildFinalResponse(string content, string? reasoningContent = null, Usage? usage = null);
}

namespace OpenAI_Compatible_API_Proxy_for_Z.Services;

public interface IOpenAIResponseBuilder
{
    OpenAIResponse BuildStreamChunk(string? content, bool isFirst = false, bool isLast = false);
    OpenAIResponse BuildFinalResponse(string content, Usage? usage = null);
}

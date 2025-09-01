
using System.Text.Json.Serialization;

namespace OpenAI_Compatible_API_Proxy_for_Z;

// OpenAI-compatible request and response structures

public record Message(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("reasoning_content")] string? ReasoningContent = null
);

public record OpenAIRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<Message> Messages,
    [property: JsonPropertyName("stream")] bool Stream,
    [property: JsonPropertyName("temperature")] double? Temperature,
    [property: JsonPropertyName("max_tokens")] int? MaxTokens
);

public record OpenAIResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("created")] long Created,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("choices")] List<Choice> Choices,
    [property: JsonPropertyName("usage")] Usage? Usage
);

public record Choice(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("message")] Message? Message,
    [property: JsonPropertyName("delta")] Delta? Delta,
    [property: JsonPropertyName("finish_reason")] string? FinishReason
);

public record Delta(
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("reasoning_content")] string? ReasoningContent = null
);

public record Usage(
    [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
    [property: JsonPropertyName("completion_tokens")] int CompletionTokens,
    [property: JsonPropertyName("total_tokens")] int TotalTokens
);

// Upstream (z.ai) request structure

public record UpstreamRequest(
    [property: JsonPropertyName("stream")] bool Stream,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<Message> Messages,
    [property: JsonPropertyName("params")] Dictionary<string, object> Params,
    [property: JsonPropertyName("features")] Dictionary<string, object> Features,
    [property: JsonPropertyName("background_tasks")] Dictionary<string, bool>? BackgroundTasks,
    [property: JsonPropertyName("chat_id")] string? ChatId,
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("mcp_servers")] List<string>? McpServers,
    [property: JsonPropertyName("model_item")] ModelItem? ModelItem,
    [property: JsonPropertyName("tool_servers")] List<string>? ToolServers,
    [property: JsonPropertyName("variables")] Dictionary<string, string>? Variables
);

public record ModelItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("owned_by")] string OwnedBy
);

// Upstream (z.ai) SSE response structure

public record UpstreamData(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("data")] UpstreamDataDetails Data,
    [property: JsonPropertyName("error")] UpstreamError? Error
);

public record UpstreamDataDetails(
    [property: JsonPropertyName("delta_content")] string DeltaContent,
    [property: JsonPropertyName("phase")] string Phase,
    [property: JsonPropertyName("done")] bool Done,
    [property: JsonPropertyName("usage")] Usage? Usage,
    [property: JsonPropertyName("error")] UpstreamError? Error,
    [property: JsonPropertyName("data")] InnerErrorContainer? Inner
);

public record InnerErrorContainer(
    [property: JsonPropertyName("error")] UpstreamError? Error
);

public record UpstreamError(
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("code")] int Code
);

// Models list response

public record ModelsResponse(
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("data")] List<ModelInfo> Data
);

public record ModelInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object")] string Object,
    [property: JsonPropertyName("created")] long Created,
    [property: JsonPropertyName("owned_by")] string OwnedBy
);

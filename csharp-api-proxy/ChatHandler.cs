using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using OpenAI_Compatible_API_Proxy_for_Z.Services;

namespace OpenAI_Compatible_API_Proxy_for_Z;

public class ChatHandler
{
    private readonly ProxySettings _settings;
    private readonly IUpstreamService _upstreamService;
    private readonly IContentTransformer _contentTransformer;
    private readonly IOpenAIResponseBuilder _responseBuilder;
    private readonly ILogger<ChatHandler> _logger;

    public ChatHandler(
        IOptions<ProxySettings> settings,
        IUpstreamService upstreamService,
        IContentTransformer contentTransformer,
        IOpenAIResponseBuilder responseBuilder,
        ILogger<ChatHandler> logger)
    {
        _settings = settings.Value;
        _upstreamService = upstreamService;
        _contentTransformer = contentTransformer;
        _responseBuilder = responseBuilder;
        _logger = logger;
    }

    public async Task Handle(HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        
        if (context.Request.Method == "OPTIONS")
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return;
        }

        // Authorization check
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.ToString().StartsWith("Bearer "))
        {
            _logger.LogWarning("缺少或无效的 Authorization 请求头");
            throw new UnauthorizedAccessException("缺少或无效的 Authorization 请求头");
        }

        var apiKey = authHeader.ToString().Substring("Bearer ".Length);
        if (apiKey != _settings.DefaultKey)
        {
            _logger.LogWarning("无效的 API 密钥: {ApiKey}", apiKey);
            throw new UnauthorizedAccessException("无效的 API 密钥");
        }

        // Decode request
        OpenAIRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<OpenAIRequest>(context.Request.Body, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
            if (request == null)
            {
                throw new JsonException("请求体为空或格式错误");
            }
            
            _logger.LogInformation("收到聊天请求, 模型: {Model}, 消息数量: {MessageCount}, 流式: {Stream}", 
                request.Model, request.Messages.Count, request.Stream);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "请求体 JSON 格式无效");
            throw;
        }

        var upstreamRequest = new UpstreamRequest(
            Stream: true, // Always stream from upstream
            ChatId: $"{DateTimeOffset.Now.ToUnixTimeMilliseconds()}-{DateTimeOffset.Now.ToUnixTimeSeconds()}",
            Id: DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString(),
            Model: "0727-360B-API",
            Messages: request.Messages,
            Params: new Dictionary<string, object>(),
            Features: new Dictionary<string, object> { { "enable_thinking", true } },
            BackgroundTasks: new Dictionary<string, bool> { { "title_generation", false }, { "tags_generation", false } },
            McpServers: new List<string>(),
            ModelItem: new ModelItem("0727-360B-API", "GLM-4.5", "openai"),
            ToolServers: new List<string>(),
            Variables: new Dictionary<string, string>
            {
                { "{{USER_NAME}}", "User" },
                { "{{USER_LOCATION}}", "Unknown" },
                { "{{CURRENT_DATETIME}}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            }
        );

        var authToken = await _upstreamService.GetAuthTokenAsync(cancellationToken);

        if (request.Stream)
        {
            await HandleStreamResponse(context, upstreamRequest, authToken, cancellationToken);
        }
        else
        {
            await HandleNonStreamResponse(context, upstreamRequest, authToken, cancellationToken);
        }
    }


    
    private async Task HandleStreamResponse(HttpContext context, UpstreamRequest upstreamRequest, string authToken, CancellationToken cancellationToken)
    {
        using var responseMessage = await _upstreamService.CallApiAsync(upstreamRequest, authToken, cancellationToken);
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        using var stream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        // First chunk
        var firstChunk = _responseBuilder.BuildStreamChunk(null, isFirst: true);
        await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(firstChunk)}\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var dataStr = line.Substring("data: ".Length);
            if (string.IsNullOrEmpty(dataStr)) continue;

            UpstreamData? upstreamData = _contentTransformer.DeserializeUpstreamData(dataStr);
            if (upstreamData == null) continue;

            if (!string.IsNullOrEmpty(upstreamData.Data.DeltaContent))
            {
                var outContent = _contentTransformer.TransformThinking(upstreamData.Data.DeltaContent, upstreamData.Data.Phase);
                if (!string.IsNullOrEmpty(outContent))
                {
                    var chunk = _responseBuilder.BuildStreamChunk(outContent);
                    await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(chunk)}\n\n", cancellationToken);
                    await context.Response.Body.FlushAsync(cancellationToken);
                }
            }

            if (upstreamData.Data.Done || upstreamData.Data.Phase == "done")
            {
                var endChunk = _responseBuilder.BuildStreamChunk(null, isLast: true);
                await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(endChunk)}\n\n", cancellationToken);
                await context.Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
                break;
            }
        }
    }

    private async Task HandleNonStreamResponse(HttpContext context, UpstreamRequest upstreamRequest, string authToken, CancellationToken cancellationToken)
    {
        using var responseMessage = await _upstreamService.CallApiAsync(upstreamRequest, authToken, cancellationToken);
        var fullContent = new StringBuilder();
        using var stream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var dataStr = line.Substring("data: ".Length);
            if (string.IsNullOrEmpty(dataStr)) continue;

            UpstreamData? upstreamData = _contentTransformer.DeserializeUpstreamData(dataStr);
            if (upstreamData == null) continue;

            if (!string.IsNullOrEmpty(upstreamData.Data.DeltaContent))
            {
                var outContent = _contentTransformer.TransformThinking(upstreamData.Data.DeltaContent, upstreamData.Data.Phase);
                if (!string.IsNullOrEmpty(outContent)) fullContent.Append(outContent);
            }

            if (upstreamData.Data.Done || upstreamData.Data.Phase == "done") break;
        }

        var finalResponse = _responseBuilder.BuildFinalResponse(fullContent.ToString());
        await context.Response.WriteAsJsonAsync(finalResponse, cancellationToken: cancellationToken);
    }
}

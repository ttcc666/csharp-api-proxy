using OpenAI_Compatible_API_Proxy_for_Z.Application.Commands;
using OpenAI_Compatible_API_Proxy_for_Z.Application.Queries;
using OpenAI_Compatible_API_Proxy_for_Z.Infrastructure.Streaming;

namespace OpenAI_Compatible_API_Proxy_for_Z.Infrastructure.Handlers;

/// <summary>
/// API 端点处理器
/// </summary>
public sealed class ApiEndpointHandler
{
    private readonly IMediator _mediator;
    private readonly ILogger<ApiEndpointHandler> _logger;

    public ApiEndpointHandler(IMediator mediator, ILogger<ApiEndpointHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleChatCompletionAsync(
        HttpContext context, 
        string requestId)
    {
        // 解析请求
        var request = await ParseRequestAsync(context);
        
        // 验证认证
        var apiKey = ExtractApiKey(context);
        
        // 使用MediatR处理命令
        var command = new ProcessChatCommand
        {
            RequestId = requestId,
            Model = request.Model,
            Messages = request.Messages,
            Stream = request.Stream,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            ApiKey = apiKey,
            CancellationToken = context.RequestAborted
        };

        var response = await _mediator.Send(command, context.RequestAborted);

        if (!response.IsSuccess)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    type = "invalid_request_error",
                    message = response.ErrorMessage,
                    code = "invalid_request",
                    request_id = requestId
                }
            });
            return;
        }

        // 处理响应
        if (request.Stream)
        {
            await HandleStreamResponseAsync(context, response, requestId);
        }
        else
        {
            await HandleNonStreamResponseAsync(context, response, requestId);
        }
    }

    private static async Task<OpenAIRequest> ParseRequestAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        var request = await JsonSerializer.DeserializeAsync<OpenAIRequest>(
            context.Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            context.RequestAborted);

        if (request == null)
        {
            throw new JsonException("请求体为空或格式错误");
        }

        return request;
    }

    private static string ExtractApiKey(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            !authHeader.ToString().StartsWith("Bearer "))
        {
            throw new UnauthorizedAccessException("缺少或无效的 Authorization 请求头");
        }

        return authHeader.ToString().Substring("Bearer ".Length);
    }

    private async Task HandleStreamResponseAsync(
        HttpContext context,
        ProcessChatResponse response,
        string requestId)
    {
        // 使用高性能流处理器
        var processor = context.RequestServices.GetRequiredService<HighPerformanceStreamProcessor>();
        var upstreamService = context.RequestServices.GetRequiredService<IUpstreamService>();

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        using var httpResponse = await upstreamService.CallApiAsync(response.UpstreamRequest, response.AuthToken, context.RequestAborted);
        using var stream = await httpResponse.Content.ReadAsStreamAsync(context.RequestAborted);

        await processor.ProcessStreamAsync(
            stream,
            async chunk => await context.Response.WriteAsync($"data: {chunk}\n\n", context.RequestAborted),
            requestId,
            context.RequestAborted);
    }

    private async Task HandleNonStreamResponseAsync(
        HttpContext context,
        ProcessChatResponse response,
        string requestId)
    {
        // 非流式响应处理逻辑
        var upstreamService = context.RequestServices.GetRequiredService<IUpstreamService>();
        var contentTransformer = context.RequestServices.GetRequiredService<IContentTransformer>();
        var responseBuilder = context.RequestServices.GetRequiredService<IOpenAIResponseBuilder>();

        using var httpResponse = await upstreamService.CallApiAsync(response.UpstreamRequest, response.AuthToken, context.RequestAborted);
        using var stream = await httpResponse.Content.ReadAsStreamAsync(context.RequestAborted);
        using var reader = new StreamReader(stream);

        var fullContent = new StringBuilder();
        var fullReasoningContent = new StringBuilder();

        while (!reader.EndOfStream && !context.RequestAborted.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(context.RequestAborted);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var dataStr = line.Substring("data: ".Length);
            if (string.IsNullOrEmpty(dataStr)) continue;

            var upstreamData = contentTransformer.DeserializeUpstreamData(dataStr);
            if (upstreamData == null) continue;

            if (!string.IsNullOrEmpty(upstreamData.Data.DeltaContent))
            {
                var (content, reasoningContent) = contentTransformer.ProcessContent(
                    upstreamData.Data.DeltaContent, upstreamData.Data.Phase);

                if (!string.IsNullOrEmpty(content)) fullContent.Append(content);
                if (!string.IsNullOrEmpty(reasoningContent)) fullReasoningContent.Append(reasoningContent);
            }

            if (upstreamData.Data.Done || upstreamData.Data.Phase == "done") break;
        }

        var finalContentString = fullContent.Length > 0 ? fullContent.ToString() : "";
        var finalReasoningContentString = fullReasoningContent.Length > 0 ? fullReasoningContent.ToString() : null;

        var finalResponse = responseBuilder.BuildFinalResponse(finalContentString, finalReasoningContentString);
        await context.Response.WriteAsJsonAsync(finalResponse, cancellationToken: context.RequestAborted);
    }
}

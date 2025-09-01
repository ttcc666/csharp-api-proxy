using OpenAI_Compatible_API_Proxy_for_Z.Application.Queries;
using OpenAI_Compatible_API_Proxy_for_Z.Infrastructure.Handlers;

namespace OpenAI_Compatible_API_Proxy_for_Z.Infrastructure.Extensions;

/// <summary>
/// API 端点配置扩展
/// </summary>
public static class EndpointExtensions
{
    /// <summary>
    /// 配置API端点
    /// </summary>
    public static WebApplication ConfigureApiEndpoints(this WebApplication app)
    {
        // 模型列表API - 使用MediatR
        app.MapGet("/v1/models", async (IMediator mediator, CancellationToken cancellationToken) =>
        {
            var query = new GetModelsQuery
            {
                IncludeDynamicModels = true,
                CancellationToken = cancellationToken
            };

            var response = await mediator.Send(query, cancellationToken);
            
            var modelsResponse = new ModelsResponse(
                Object: "list",
                Data: response.Models.ToList()
            );

            return Results.Ok(modelsResponse);
        })
        .WithName("GetModels")
        .WithTags("Models")
        .WithSummary("获取可用模型列表")
        .WithDescription("返回所有可用的AI模型列表，包括动态模型")
        .Produces<ModelsResponse>(200)
        .Produces(500);

        // 聊天完成API - 使用现代化处理器
        app.MapMethods("/v1/chat/completions", new[] { "POST", "OPTIONS" }, 
            async (HttpContext context, ApiEndpointHandler handler, ILogger<ApiEndpointHandler> logger) =>
        {
            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return;
            }

            var requestId = Guid.NewGuid().ToString("N")[..8];
            context.Response.Headers["X-Request-ID"] = requestId;

            try
            {
                // 使用新的现代化聊天处理器
                await handler.HandleChatCompletionAsync(context, requestId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "聊天完成API处理失败 [RequestId: {RequestId}]", requestId);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        type = "api_error",
                        message = "内部服务器错误",
                        code = "internal_error",
                        request_id = requestId
                    }
                });
            }
        })
        .WithName("ChatCompletions")
        .WithTags("Chat")
        .WithSummary("创建聊天完成")
        .WithDescription("使用指定模型创建聊天完成，支持流式和非流式响应")
        .Accepts<OpenAIRequest>("application/json")
        .Produces<OpenAIResponse>(200)
        .Produces(400)
        .Produces(401)
        .Produces(500);

        return app;
    }
}

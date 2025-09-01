using System.Net;
using System.Text.Json;

namespace OpenAI_Compatible_API_Proxy_for_Z.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发生未处理的异常: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            ArgumentException _ => new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Type = "invalid_request_error",
                    Message = exception.Message,
                    Code = "invalid_request"
                }
            },
            UnauthorizedAccessException _ => new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Type = "authentication_error",
                    Message = "身份验证失败",
                    Code = "invalid_api_key"
                }
            },
            HttpRequestException httpEx => new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Type = "api_error",
                    Message = "上游服务请求失败",
                    Code = "upstream_error"
                }
            },
            TaskCanceledException _ => new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Type = "timeout_error",
                    Message = "请求超时",
                    Code = "timeout"
                }
            },
            JsonException _ => new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Type = "invalid_request_error",
                    Message = "JSON 格式错误",
                    Code = "invalid_json"
                }
            },
            _ => new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Type = "api_error",
                    Message = "内部服务器错误",
                    Code = "internal_error"
                }
            }
        };

        context.Response.StatusCode = exception switch
        {
            ArgumentException => (int)HttpStatusCode.BadRequest,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            HttpRequestException => (int)HttpStatusCode.BadGateway,
            TaskCanceledException => (int)HttpStatusCode.RequestTimeout,
            JsonException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}

public record ErrorResponse
{
    public required ErrorDetails Error { get; init; }
}

public record ErrorDetails
{
    public required string Type { get; init; }
    public required string Message { get; init; }
    public required string Code { get; init; }
    public string? Param { get; init; }
}

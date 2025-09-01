using System.Text;
using System.Text.Json;

namespace OpenAI_Compatible_API_Proxy_for_Z.Middleware;

/// <summary>
/// 专门的请求日志中间件 - 记录每个请求的详细信息
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 简化的请求日志记录，避免流处理问题
        try
        {
            // 记录详细的请求信息（不读取请求体，避免阻塞）
            LogRequestDetails(context, string.Empty);

            await _next(context);

            // 记录简单的响应信息
            LogSimpleResponseDetails(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "🔥 请求处理失败: {Method} {Path} | RequestId: {RequestId} | 异常: {Message}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier,
                ex.Message);
            throw;
        }
    }

    private void LogRequestDetails(HttpContext context, string requestBody)
    {
        var requestInfo = new
        {
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff UTC"),
            RequestId = context.TraceIdentifier,
            Method = context.Request.Method,
            Scheme = context.Request.Scheme,
            Host = context.Request.Host.ToString(),
            Path = context.Request.Path.ToString(),
            QueryString = context.Request.QueryString.ToString(),
            ContentType = context.Request.ContentType,
            ContentLength = context.Request.ContentLength,
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            Referer = context.Request.Headers.Referer.ToString(),
            ClientIP = GetClientIP(context),
            Headers = GetSafeHeaders(context.Request.Headers),
            HasBody = !string.IsNullOrEmpty(requestBody)
        };

        _logger.LogInformation(
            "📥 请求详情: {RequestInfo}",
            JsonSerializer.Serialize(requestInfo, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));

        // 在调试模式下记录是否有请求体
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var hasBody = context.Request.ContentLength > 0;
            _logger.LogDebug(
                "📝 请求体信息: 有内容={HasBody}, 大小={ContentLength} [RequestId: {RequestId}]",
                hasBody,
                context.Request.ContentLength ?? 0,
                context.TraceIdentifier);
        }
    }

    private void LogSimpleResponseDetails(HttpContext context)
    {
        var responseInfo = new
        {
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff UTC"),
            RequestId = context.TraceIdentifier,
            StatusCode = context.Response.StatusCode,
            ContentType = context.Response.ContentType,
            ContentLength = context.Response.ContentLength,
            HasBody = context.Response.ContentLength > 0
        };

        _logger.LogInformation(
            "📤 响应详情: {ResponseInfo}",
            JsonSerializer.Serialize(responseInfo, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }));
    }



    private static string GetClientIP(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    private static Dictionary<string, string> GetSafeHeaders(IHeaderDictionary headers)
    {
        var safeHeaders = new Dictionary<string, string>();
        var excludeHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization", "Cookie", "Set-Cookie", "X-API-Key", "Bearer"
        };

        foreach (var header in headers.Take(20)) // 限制数量
        {
            if (!excludeHeaders.Contains(header.Key))
            {
                var value = string.Join(", ", header.Value.ToArray());
                safeHeaders[header.Key] = value.Length > 100 ? value[..100] + "..." : value;
            }
            else
            {
                safeHeaders[header.Key] = "[已隐藏]";
            }
        }

        return safeHeaders;
    }
}

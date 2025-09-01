using System.Diagnostics;

namespace OpenAI_Compatible_API_Proxy_for_Z.Middleware;

/// <summary>
/// 性能监控中间件 - 记录请求处理时间和性能指标
/// </summary>
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
    private static readonly ActivitySource ActivitySource = new("OpenAI.Proxy.Performance");

    public PerformanceMonitoringMiddleware(RequestDelegate next, ILogger<PerformanceMonitoringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var activity = ActivitySource.StartActivity("Request.Performance");
        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;

        try
        {
            // 记录请求开始
            var requestInfo = new
            {
                Method = context.Request.Method,
                Path = context.Request.Path,
                ContentLength = context.Request.ContentLength,
                UserAgent = context.Request.Headers.UserAgent.ToString()
            };

            activity?.SetTag("request.method", context.Request.Method);
            activity?.SetTag("request.path", context.Request.Path);
            activity?.SetTag("request.id", requestId);

            // 详细记录每个请求的开始
            _logger.LogInformation("🚀 请求开始: {Method} {Path}{QueryString} | RequestId: {RequestId} | 客户端IP: {ClientIP} | UserAgent: {UserAgent} | ContentLength: {ContentLength}",
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString,
                requestId,
                GetClientIP(context),
                context.Request.Headers.UserAgent.ToString(),
                context.Request.ContentLength);

            // 调试模式下记录请求头
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var headers = string.Join(", ", context.Request.Headers.Take(10).Select(h => $"{h.Key}={string.Join(";", h.Value.Take(1))}"));
                _logger.LogDebug("📋 请求头: {Headers} [RequestId: {RequestId}]", headers, requestId);
            }

            // 执行下一个中间件
            await _next(context);

            // 记录请求完成
            stopwatch.Stop();
            var duration = stopwatch.Elapsed;

            var responseInfo = new
            {
                StatusCode = context.Response.StatusCode,
                ContentLength = context.Response.ContentLength,
                Duration = duration.TotalMilliseconds
            };

            activity?.SetTag("response.status_code", context.Response.StatusCode);
            activity?.SetTag("response.duration_ms", duration.TotalMilliseconds);

            // 详细记录每个请求的完成
            var statusIcon = context.Response.StatusCode < 400 ? "✅" : "❌";
            var sizeInfo = context.Response.ContentLength.HasValue ? $"{context.Response.ContentLength.Value}字节" : "未知大小";
            
            _logger.LogInformation("{Icon} 请求完成: {Method} {Path}{QueryString} | 状态码: {StatusCode} | 耗时: {Duration}ms | 响应大小: {ResponseSize} | RequestId: {RequestId}",
                statusIcon,
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString,
                context.Response.StatusCode,
                duration.TotalMilliseconds,
                sizeInfo,
                requestId);

            // 性能分级警告
            if (duration.TotalMilliseconds > 10000) // 超过10秒 - 严重
            {
                _logger.LogError("🐌 严重慢请求: {Method} {Path} 耗时 {Duration}ms [RequestId: {RequestId}]", 
                    context.Request.Method, context.Request.Path, duration.TotalMilliseconds, requestId);
            }
            else if (duration.TotalMilliseconds > 5000) // 超过5秒 - 警告
            {
                _logger.LogWarning("⚠️  慢请求警告: {Method} {Path} 耗时 {Duration}ms [RequestId: {RequestId}]", 
                    context.Request.Method, context.Request.Path, duration.TotalMilliseconds, requestId);
            }
            else if (duration.TotalMilliseconds > 2000) // 超过2秒 - 注意
            {
                _logger.LogInformation("⏱️  注意请求: {Method} {Path} 耗时 {Duration}ms [RequestId: {RequestId}]", 
                    context.Request.Method, context.Request.Path, duration.TotalMilliseconds, requestId);
            }

            // 调试模式下记录响应头
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var responseHeaders = string.Join(", ", context.Response.Headers.Take(10).Select(h => $"{h.Key}={string.Join(";", h.Value.Take(1))}"));
                _logger.LogDebug("📤 响应头: {ResponseHeaders} [RequestId: {RequestId}]", responseHeaders, requestId);
            }

            // 记录性能指标
            RecordPerformanceMetrics(context, duration);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed;

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("response.duration_ms", duration.TotalMilliseconds);

            _logger.LogError(ex, "请求处理异常 [RequestId: {RequestId}] [Duration: {Duration}ms] [Error: {Error}]", 
                requestId, duration.TotalMilliseconds, ex.Message);

            throw;
        }
    }

    private void RecordPerformanceMetrics(HttpContext context, TimeSpan duration)
    {
        // 这里可以添加更多的性能指标收集
        // 例如：发送到 Prometheus、Application Insights 等

        if (context.Request.Path.StartsWithSegments("/v1/chat/completions"))
        {
            var isStream = context.Request.Query.ContainsKey("stream") || 
                          (context.Request.ContentType?.Contains("application/json") == true);

            _logger.LogInformation("📊 性能指标 - ChatCompletion: 耗时={Duration}ms, 流式={IsStream}, 状态码={StatusCode}, 大小={ContentLength}",
                duration.TotalMilliseconds, isStream, context.Response.StatusCode, context.Response.ContentLength);
        }
    }

    private static string GetClientIP(HttpContext context)
    {
        // 尝试从各种头部获取真实IP
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
}


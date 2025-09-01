using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenAI_Compatible_API_Proxy_for_Z.Services;

namespace OpenAI_Compatible_API_Proxy_for_Z.HealthChecks;

public class UpstreamHealthCheck : IHealthCheck
{
    private readonly IUpstreamService _upstreamService;
    private readonly ProxySettings _settings;
    private readonly ILogger<UpstreamHealthCheck> _logger;

    public UpstreamHealthCheck(
        IUpstreamService upstreamService,
        IOptions<ProxySettings> settings,
        ILogger<UpstreamHealthCheck> logger)
    {
        _upstreamService = upstreamService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // 测试获取认证令牌
            var token = await _upstreamService.GetAuthTokenAsync(cancellationToken);
            
            if (string.IsNullOrEmpty(token))
            {
                return HealthCheckResult.Unhealthy("无法获取上游服务认证令牌");
            }

            // 可以添加更多的健康检查逻辑，比如发送测试请求
            var data = new Dictionary<string, object>
            {
                ["upstream_url"] = _settings.UpstreamUrl,
                ["anon_token_enabled"] = _settings.AnonTokenEnabled,
                ["has_token"] = !string.IsNullOrEmpty(token)
            };

            return HealthCheckResult.Healthy("上游服务连接正常", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上游服务健康检查失败");
            return HealthCheckResult.Unhealthy("上游服务连接失败", ex);
        }
    }
}

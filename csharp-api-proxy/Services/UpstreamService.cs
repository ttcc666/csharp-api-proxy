using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OpenAI_Compatible_API_Proxy_for_Z.Services;

public class UpstreamService : IUpstreamService
{
    private readonly ProxySettings _settings;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UpstreamService> _logger;

    public UpstreamService(
        IOptions<ProxySettings> settings,
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<UpstreamService> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> GetAuthTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.AnonTokenEnabled)
        {
            return _settings.UpstreamToken;
        }

        const string cacheKey = "anon_token";
        if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            _logger.LogDebug("使用缓存的匿名令牌");
            return cachedToken;
        }

        try
        {
            _logger.LogInformation("获取新的匿名令牌");
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.OriginBase}/api/v1/auths/");
            AddBrowserHeaders(request, _settings.OriginBase);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var token = body.GetProperty("token").GetString();
            
            if (!string.IsNullOrEmpty(token))
            {
                var cacheExpiry = TimeSpan.FromMinutes(_settings.TokenCacheMinutes);
                _cache.Set(cacheKey, token, cacheExpiry);
                _logger.LogInformation("匿名令牌获取成功并已缓存 {Minutes} 分钟", _settings.TokenCacheMinutes);
                return token;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取匿名令牌失败，使用默认令牌");
        }

        return _settings.UpstreamToken;
    }

    public async Task<HttpResponseMessage> CallApiAsync(UpstreamRequest request, string token, CancellationToken cancellationToken = default)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, _settings.UpstreamUrl);
        
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        AddBrowserHeaders(requestMessage, $"{_settings.OriginBase}/c/{request.ChatId}");
        
        var jsonContent = JsonSerializer.Serialize(request);
        requestMessage.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogDebug("发送上游API请求到 {Url}", _settings.UpstreamUrl);
        
        var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        return response;
    }

    private void AddBrowserHeaders(HttpRequestMessage request, string referer)
    {
        request.Headers.Add("User-Agent", _settings.BrowserUa);
        request.Headers.Add("Accept", "*/*");
        request.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9");
        request.Headers.Add("X-FE-Version", _settings.XFeVersion);
        request.Headers.Add("sec-ch-ua", _settings.SecChUa);
        request.Headers.Add("sec-ch-ua-mobile", _settings.SecChUaMob);
        request.Headers.Add("sec-ch-ua-platform", _settings.SecChUaPlat);
        request.Headers.Add("Origin", _settings.OriginBase);
        request.Headers.Add("Referer", referer);
    }
}

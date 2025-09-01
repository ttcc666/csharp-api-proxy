using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenAI_Compatible_API_Proxy_for_Z.Services;

/// <summary>
/// 动态模型服务实现 - 从上游API获取可用模型（基于Python版本逻辑）
/// </summary>
public class DynamicModelService : IDynamicModelService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUpstreamService _upstreamService;
    private readonly ProxySettings _settings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DynamicModelService> _logger;
    
    private const string CacheKey = "dynamic_models";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(30);

    public DynamicModelService(
        IHttpClientFactory httpClientFactory,
        IUpstreamService upstreamService,
        IOptions<ProxySettings> settings,
        IMemoryCache cache,
        ILogger<DynamicModelService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _upstreamService = upstreamService;
        _settings = settings.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IList<DynamicModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        // 尝试从缓存获取
        if (_cache.TryGetValue<IList<DynamicModelInfo>>(CacheKey, out var cachedModels))
        {
            _logger.LogDebug("从缓存返回 {Count} 个模型", cachedModels?.Count ?? 0);
            return cachedModels ?? new List<DynamicModelInfo>();
        }

        try
        {
            var models = await FetchModelsFromUpstreamAsync(cancellationToken);
            
            // 添加我们的智能调度模型
            var enhancedModels = new List<DynamicModelInfo>(models)
            {
                new DynamicModelInfo(
                    Id: ModelNames.Auto,
                    Name: "GLM-4.5-Auto",
                    Object: "model",
                    Created: DateTimeOffset.Now.ToUnixTimeSeconds(),
                    OwnedBy: "z.ai"
                )
            };

            // 缓存结果
            _cache.Set(CacheKey, enhancedModels, CacheExpiry);
            
            _logger.LogInformation("动态获取到 {Count} 个模型（包含智能调度模型）", enhancedModels.Count);
            return enhancedModels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "动态模型获取失败，回退到静态模型列表");
            return GetFallbackModels();
        }
    }

    public async Task RefreshModelCacheAsync(CancellationToken cancellationToken = default)
    {
        _cache.Remove(CacheKey);
        await GetAvailableModelsAsync(cancellationToken);
        _logger.LogInformation("模型缓存已刷新");
    }

    public async Task<bool> IsValidModelAsync(string modelId)
    {
        var models = await GetAvailableModelsAsync();
        return models.Any(m => m.Id == modelId && m.IsActive);
    }

    private async Task<IList<DynamicModelInfo>> FetchModelsFromUpstreamAsync(CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        
        // 设置请求头（基于Python版本）
        var authToken = await _upstreamService.GetAuthTokenAsync(cancellationToken);
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");
        httpClient.DefaultRequestHeaders.Add("User-Agent", _settings.BrowserUa);
        httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
        httpClient.DefaultRequestHeaders.Add("X-FE-Version", _settings.XFeVersion);
        httpClient.DefaultRequestHeaders.Add("sec-ch-ua", _settings.SecChUa);
        httpClient.DefaultRequestHeaders.Add("sec-ch-ua-mobile", _settings.SecChUaMob);
        httpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform", _settings.SecChUaPlat);
        httpClient.DefaultRequestHeaders.Add("Origin", _settings.OriginBase);

        var response = await httpClient.GetAsync($"{_settings.OriginBase}/api/models", cancellationToken);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseData = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

        var models = new List<DynamicModelInfo>();
        
        if (responseData.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var modelElement in dataArray.EnumerateArray())
            {
                if (!modelElement.TryGetProperty("info", out var info) ||
                    !info.TryGetProperty("is_active", out var isActiveElement) ||
                    !isActiveElement.GetBoolean())
                {
                    continue; // 跳过非活动模型
                }

                var modelId = modelElement.GetProperty("id").GetString() ?? "";
                var modelName = modelElement.TryGetProperty("name", out var nameElement) 
                    ? nameElement.GetString() ?? "" 
                    : "";

                // 应用Python版本的模型名称格式化规则
                var formattedName = FormatModelName(modelId, modelName);

                var createdAt = info.TryGetProperty("created_at", out var createdElement) 
                    ? createdElement.GetInt64() 
                    : DateTimeOffset.Now.ToUnixTimeSeconds();

                models.Add(new DynamicModelInfo(
                    Id: modelId,
                    Name: formattedName,
                    Object: "model",
                    Created: createdAt,
                    OwnedBy: "z.ai"
                ));
            }
        }

        return models;
    }

    /// <summary>
    /// 格式化模型名称（基于Python版本的逻辑）
    /// </summary>
    private static string FormatModelName(string modelId, string modelName)
    {
        // 如果模型名为空或不是英文字母开头，使用格式化的模型ID
        if (string.IsNullOrEmpty(modelName) || !IsEnglishLetter(modelName[0]))
        {
            return FormatModelId(modelId);
        }

        // 如果模型ID以GLM或Z开头，直接使用模型ID
        if (modelId.StartsWith("GLM") || modelId.StartsWith("Z"))
        {
            return modelId;
        }

        return modelName;
    }

    /// <summary>
    /// 格式化模型ID（基于Python版本的规则）
    /// </summary>
    private static string FormatModelId(string modelId)
    {
        if (string.IsNullOrEmpty(modelId)) return "";

        var parts = modelId.Split('-');
        if (parts.Length == 1)
        {
            return parts[0].ToUpper();
        }

        var formatted = new List<string> { parts[0].ToUpper() };
        
        for (int i = 1; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part))
            {
                formatted.Add("");
            }
            else if (part.All(char.IsDigit))
            {
                formatted.Add(part);
            }
            else if (part.Any(char.IsLetter))
            {
                formatted.Add(char.ToUpper(part[0]) + part[1..].ToLower());
            }
            else
            {
                formatted.Add(part);
            }
        }

        return string.Join("-", formatted);
    }

    /// <summary>
    /// 判断是否是英文字母
    /// </summary>
    private static bool IsEnglishLetter(char ch)
    {
        return (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z');
    }

    /// <summary>
    /// 获取回退模型列表（当动态获取失败时使用）
    /// </summary>
    private static IList<DynamicModelInfo> GetFallbackModels()
    {
        return new List<DynamicModelInfo>
        {
            new(ModelNames.Default, "GLM-4.5", "model", DateTimeOffset.Now.ToUnixTimeSeconds(), "z.ai"),
            new(ModelNames.Thinking, "GLM-4.5-Thinking", "model", DateTimeOffset.Now.ToUnixTimeSeconds(), "z.ai"),
            new(ModelNames.Search, "GLM-4.5-Search", "model", DateTimeOffset.Now.ToUnixTimeSeconds(), "z.ai"),
            new(ModelNames.Auto, "GLM-4.5-Auto", "model", DateTimeOffset.Now.ToUnixTimeSeconds(), "z.ai")
        };
    }
}


using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace OpenAI_Compatible_API_Proxy_for_Z.Services;

public class ContentTransformer : IContentTransformer
{
    private readonly ProxySettings _settings;
    private readonly ILogger<ContentTransformer> _logger;

    public ContentTransformer(IOptions<ProxySettings> settings, ILogger<ContentTransformer> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public string TransformThinking(string content, string phase)
    {
        if (phase != "thinking") return content;

        // 与Go版本完全一致的转换逻辑
        var transformed = Regex.Replace(content, "(?s)<summary>.*?</summary>", "");
        
        // 清理残留自定义标签，如 </thinking>、<Full> 等（与Go版本一致）
        transformed = transformed.Replace("</thinking>", "")
                                .Replace("<Full>", "")
                                .Replace("</Full>", "")
                                .Trim();

        // 标签处理策略（与Go版本一致）
        switch (_settings.ThinkTagsMode)
        {
            case "think":
                transformed = Regex.Replace(transformed, "<details[^>]*>", "<think>");
                transformed = transformed.Replace("</details>", "</think>");
                break;
            case "strip":
                transformed = Regex.Replace(transformed, "<details[^>]*>", "");
                transformed = transformed.Replace("</details>", "");
                break;
            case "keep":
                // 保持原样
                break;
        }

        // 处理每行前缀 "> "（包括起始位置）- 与Go版本一致
        transformed = transformed.TrimStart();
        if (transformed.StartsWith("> "))
        {
            transformed = transformed.Substring(2);
        }
        transformed = transformed.Replace("\n> ", "\n");

        return transformed.Trim();
    }

    public (string? content, string? reasoningContent) ProcessContent(string deltaContent, string phase)
    {
        if (phase == "thinking")
        {
            var transformedThinking = TransformThinking(deltaContent, phase);
            return (null, string.IsNullOrEmpty(transformedThinking) ? null : transformedThinking);
        }
        else
        {
            return (string.IsNullOrEmpty(deltaContent) ? null : deltaContent, null);
        }
    }

    public UpstreamData? DeserializeUpstreamData(string dataStr)
    {
        try
        {
            return JsonSerializer.Deserialize<UpstreamData>(dataStr);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "反序列化上游数据失败: {Data}", dataStr);
            return null;
        }
    }
}

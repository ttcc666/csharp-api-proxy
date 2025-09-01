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

        var transformed = Regex.Replace(content, "(?s)<summary>.*?</summary>", "");
        transformed = transformed.Replace("</thinking>", "").Replace("<Full>", "").Replace("</Full>", "").Trim();

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

        if (transformed.StartsWith("> ")) 
            transformed = transformed.Substring(2);
        transformed = transformed.Replace("\n> ", "\n");

        return transformed.Trim();
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

namespace OpenAI_Compatible_API_Proxy_for_Z.Domain.ValueObjects;

/// <summary>
/// 聊天功能配置值对象
/// </summary>
public sealed record ChatFeatures
{
    /// <summary>
    /// 是否启用思考功能
    /// </summary>
    public bool EnableThinking { get; init; }

    /// <summary>
    /// 是否启用网络搜索
    /// </summary>
    public bool EnableWebSearch { get; init; }

    /// <summary>
    /// 是否启用自动网络搜索
    /// </summary>
    public bool EnableAutoWebSearch { get; init; }

    /// <summary>
    /// MCP服务器列表
    /// </summary>
    public IReadOnlyList<string> McpServers { get; init; } = [];

    /// <summary>
    /// 创建基本功能配置
    /// </summary>
    public static ChatFeatures Basic() => new();

    /// <summary>
    /// 创建思考功能配置
    /// </summary>
    public static ChatFeatures WithThinking() => new()
    {
        EnableThinking = true
    };

    /// <summary>
    /// 创建搜索功能配置
    /// </summary>
    public static ChatFeatures WithSearch(string mcpServer = "deep-web-search") => new()
    {
        EnableThinking = true,
        EnableWebSearch = true,
        EnableAutoWebSearch = true,
        McpServers = [mcpServer]
    };

    /// <summary>
    /// 转换为字典格式（用于上游API）
    /// </summary>
    public Dictionary<string, object> ToFeaturesDictionary() => new()
    {
        { "enable_thinking", EnableThinking },
        { "web_search", EnableWebSearch },
        { "auto_web_search", EnableAutoWebSearch }
    };
}

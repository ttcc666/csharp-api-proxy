using System.ComponentModel.DataAnnotations;

namespace OpenAI_Compatible_API_Proxy_for_Z.Configuration;

/// <summary>
/// 意图分析配置设置
/// </summary>
public class IntentAnalysisSettings
{
    /// <summary>
    /// 是否启用智能调度
    /// </summary>
    public bool EnableSmartDispatch { get; set; } = true;
    
    /// <summary>
    /// 是否启用高级分析器
    /// </summary>
    public bool UseAdvancedAnalyzer { get; set; } = true;
    
    /// <summary>
    /// 搜索意图阈值
    /// </summary>
    [Range(0.1, 10.0)]
    public float SearchThreshold { get; set; } = 2.0f;
    
    /// <summary>
    /// 思考意图阈值
    /// </summary>
    [Range(0.1, 10.0)]
    public float ThinkingThreshold { get; set; } = 2.5f;
    
    /// <summary>
    /// 组合意图阈值
    /// </summary>
    [Range(0.1, 20.0)]
    public float CombinedThreshold { get; set; } = 4.0f;
    
    /// <summary>
    /// 分析结果缓存时间（分钟）
    /// </summary>
    [Range(1, 60)]
    public int CacheTimeoutMinutes { get; set; } = 5;
    
    /// <summary>
    /// 上下文分析深度（考虑前几条消息）
    /// </summary>
    [Range(1, 10)]
    public int ContextDepth { get; set; } = 3;
    
    /// <summary>
    /// 自定义搜索关键词（将与默认关键词合并）
    /// </summary>
    public Dictionary<string, float> CustomSearchKeywords { get; set; } = new();
    
    /// <summary>
    /// 自定义思考关键词（将与默认关键词合并）
    /// </summary>
    public Dictionary<string, float> CustomThinkingKeywords { get; set; } = new();
    
    /// <summary>
    /// 禁用的关键词（不参与分析）
    /// </summary>
    public List<string> DisabledKeywords { get; set; } = new();
    
    /// <summary>
    /// 强制搜索的内容模式（正则表达式）
    /// </summary>
    public List<string> ForceSearchPatterns { get; set; } = new();
    
    /// <summary>
    /// 强制思考的内容模式（正则表达式）
    /// </summary>
    public List<string> ForceThinkingPatterns { get; set; } = new();
    
    /// <summary>
    /// 调试模式 - 输出详细分析信息
    /// </summary>
    public bool DebugMode { get; set; } = false;
}

/// <summary>
/// 工具配置设置
/// </summary>
public class ToolSettings
{
    /// <summary>
    /// 默认启用的工具
    /// </summary>
    public List<string> DefaultEnabledTools { get; set; } = new();
    
    /// <summary>
    /// 工具权重配置
    /// </summary>
    public Dictionary<string, float> ToolWeights { get; set; } = new()
    {
        { "thinking", 1.0f },
        { "search", 1.0f },
        { "analysis", 1.0f }
    };
    
    /// <summary>
    /// 搜索工具特定配置
    /// </summary>
    public SearchToolSettings Search { get; set; } = new();
    
    /// <summary>
    /// 思考工具特定配置
    /// </summary>
    public ThinkingToolSettings Thinking { get; set; } = new();
}

/// <summary>
/// 搜索工具配置
/// </summary>
public class SearchToolSettings
{
    /// <summary>
    /// 默认搜索深度
    /// </summary>
    public int DefaultSearchDepth { get; set; } = 5;
    
    /// <summary>
    /// 自动搜索关键词权重
    /// </summary>
    public float AutoSearchWeight { get; set; } = 1.0f;
    
    /// <summary>
    /// 时间敏感性权重
    /// </summary>
    public float TimeSensitivityWeight { get; set; } = 1.5f;
}

/// <summary>
/// 思考工具配置
/// </summary>
public class ThinkingToolSettings
{
    /// <summary>
    /// 默认思考深度
    /// </summary>
    public string DefaultThinkingDepth { get; set; } = "medium";
    
    /// <summary>
    /// 复杂性检测阈值
    /// </summary>
    public float ComplexityThreshold { get; set; } = 0.7f;
    
    /// <summary>
    /// 分析权重
    /// </summary>
    public float AnalysisWeight { get; set; } = 1.0f;
}

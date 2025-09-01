using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenAI_Compatible_API_Proxy_for_Z.Configuration;
using OpenAI_Compatible_API_Proxy_for_Z.Domain.Services;
using OpenAI_Compatible_API_Proxy_for_Z.Domain.ValueObjects;
using System.Text.RegularExpressions;

namespace OpenAI_Compatible_API_Proxy_for_Z.Infrastructure.Services;

/// <summary>
/// 现代化意图分析服务实现（同时实现新旧接口以保证兼容性）
/// </summary>
public sealed class IntentAnalysisService : IIntentAnalysisService, IIntentAnalyzer
{
    private readonly IntentAnalysisSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<IntentAnalysisService> _logger;

    // 预编译的正则表达式以提高性能
    private static readonly Regex TimeRelatedPattern = new(
        @"\b(今天|现在|最新|最近|当前|实时|today|now|latest|recent|current|real-time)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ComplexAnalysisPattern = new(
        @"\b(分析.*?影响|解释.*?原理|比较.*?区别|评估.*?效果|深入.*?研究|详细.*?说明|analyze.*?impact|explain.*?principle)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ExplicitSearchPattern = new(
        @"\b(搜索.*?信息|查找.*?资料|search\s+for|find\s+information|最新.*?消息|最近.*?发展)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 关键词配置
    private readonly string[] _searchKeywords;
    private readonly string[] _thinkingKeywords;
    private readonly Dictionary<string, double> _customSearchWeights;
    private readonly Dictionary<string, double> _customThinkingWeights;

    public IntentAnalysisService(
        IOptions<IntentAnalysisSettings> settings,
        IMemoryCache cache,
        ILogger<IntentAnalysisService> logger)
    {
        _settings = settings.Value;
        _cache = cache;
        _logger = logger;

        // 初始化关键词
        _searchKeywords = GetSearchKeywords();
        _thinkingKeywords = GetThinkingKeywords();
        _customSearchWeights = _settings.CustomSearchKeywords?.ToDictionary(kv => kv.Key, kv => (double)kv.Value) ?? new();
        _customThinkingWeights = _settings.CustomThinkingKeywords?.ToDictionary(kv => kv.Key, kv => (double)kv.Value) ?? new();
    }

    public ChatFeatures AnalyzeIntent(IReadOnlyList<Message> messages, string model)
    {
        if (messages.Count == 0)
        {
            return ChatFeatures.Basic();
        }

        // 检查是否需要智能调度
        if (!ModelNames.RequiresSmartDispatch(model))
        {
            return GetFeaturesForModel(model);
        }

        // 分析最后一条用户消息
        var lastUserMessage = messages.LastOrDefault(m => m.Role == "user");
        if (lastUserMessage == null)
        {
            return ChatFeatures.Basic();
        }

        return AnalyzeIntentCore(lastUserMessage.Content, messages);
    }

    public IntentAnalysisResult AnalyzeSingleMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new IntentAnalysisResult(UserIntent.Basic, false, false, "空内容");
        }

        var cacheKey = $"intent_analysis_{content.GetHashCode()}";
        
        if (_cache.TryGetValue(cacheKey, out IntentAnalysisResult? cachedResult) && cachedResult != null)
        {
            _logger.LogDebug("使用缓存的意图分析结果 [Content: {Content}]", 
                content.Length > 50 ? content[..50] + "..." : content);
            return cachedResult;
        }

        var result = AnalyzeIntentCore(content, []);
        var analysisResult = new IntentAnalysisResult(
            GetUserIntent(result), 
            result.EnableThinking, 
            result.EnableWebSearch, 
            "智能分析");

        // 缓存结果
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.CacheTimeoutMinutes),
            Size = 1
        };
        _cache.Set(cacheKey, analysisResult, cacheOptions);

        return analysisResult;
    }

    private ChatFeatures AnalyzeIntentCore(string content, IReadOnlyList<Message> messages)
    {
        var contentLower = content.ToLowerInvariant();
        
        // 强制模式检查
        if (CheckForcePatterns(contentLower, out var forcedFeatures))
        {
            return forcedFeatures;
        }

        // 计算各项分数
        var searchScore = CalculateSearchScore(contentLower);
        var thinkingScore = CalculateThinkingScore(contentLower);

        // 应用上下文权重
        if (messages.Count > 1)
        {
            var contextBonus = AnalyzeContext(messages);
            searchScore += contextBonus.SearchBonus;
            thinkingScore += contextBonus.ThinkingBonus;
        }

        // 应用长度权重
        if (content.Length > 100)
        {
            thinkingScore += 0.5;
        }

        // 决策逻辑
        var requiresSearch = searchScore >= _settings.SearchThreshold;
        var requiresThinking = thinkingScore >= _settings.ThinkingThreshold;

        // 组合模式检查
        if (requiresSearch && requiresThinking && 
            (searchScore + thinkingScore) >= _settings.CombinedThreshold)
        {
            _logger.LogInformation("检测到组合模式 [Content: {Content}] [SearchScore: {SearchScore}] [ThinkingScore: {ThinkingScore}]",
                content.Length > 100 ? content[..100] + "..." : content, searchScore, thinkingScore);
            return ChatFeatures.WithSearch();
        }

        if (requiresSearch)
        {
            _logger.LogInformation("检测到搜索需求 [Content: {Content}] [Score: {Score}]",
                content.Length > 100 ? content[..100] + "..." : content, searchScore);
            return ChatFeatures.WithSearch();
        }

        if (requiresThinking)
        {
            _logger.LogInformation("检测到思考需求 [Content: {Content}] [Score: {Score}]",
                content.Length > 100 ? content[..100] + "..." : content, thinkingScore);
            return ChatFeatures.WithThinking();
        }

        _logger.LogDebug("基础对话模式 [Content: {Content}] [SearchScore: {SearchScore}] [ThinkingScore: {ThinkingScore}]",
            content.Length > 100 ? content[..100] + "..." : content, searchScore, thinkingScore);
        return ChatFeatures.Basic();
    }

    private bool CheckForcePatterns(string content, out ChatFeatures features)
    {
        features = ChatFeatures.Basic();

        // 检查强制搜索模式
        foreach (var pattern in _settings.ForceSearchPatterns ?? [])
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
            {
                features = ChatFeatures.WithSearch();
                return true;
            }
        }

        // 检查强制思考模式
        foreach (var pattern in _settings.ForceThinkingPatterns ?? [])
        {
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
            {
                features = ChatFeatures.WithThinking();
                return true;
            }
        }

        return false;
    }

    private double CalculateSearchScore(string content)
    {
        double score = 0;

        // 基础关键词匹配
        score += CountKeywords(content, _searchKeywords);

        // 自定义权重关键词
        foreach (var (keyword, weight) in _customSearchWeights)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                score += weight;
            }
        }

        // 正则表达式模式
        if (TimeRelatedPattern.IsMatch(content))
        {
            score += 2.0;
        }

        if (ExplicitSearchPattern.IsMatch(content))
        {
            score += 3.0;
        }

        return score;
    }

    private double CalculateThinkingScore(string content)
    {
        double score = 0;

        // 基础关键词匹配
        score += CountKeywords(content, _thinkingKeywords);

        // 自定义权重关键词
        foreach (var (keyword, weight) in _customThinkingWeights)
        {
            if (content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                score += weight;
            }
        }

        // 复杂分析模式
        if (ComplexAnalysisPattern.IsMatch(content))
        {
            score += 3.0;
        }

        return score;
    }

    private (double SearchBonus, double ThinkingBonus) AnalyzeContext(IReadOnlyList<Message> messages)
    {
        double searchBonus = 0;
        double thinkingBonus = 0;

        // 分析最近几条消息的上下文
        var recentMessages = messages.TakeLast(_settings.ContextDepth).ToList();
        
        foreach (var message in recentMessages)
        {
            if (message.Role == "assistant" && !string.IsNullOrEmpty(message.Content))
            {
                // 如果助手之前提供了需要进一步搜索的信息
                if (message.Content.Contains("需要更多信息") || message.Content.Contains("最新数据"))
                {
                    searchBonus += 1.0;
                }

                // 如果助手之前提到了复杂概念
                if (message.Content.Length > 200)
                {
                    thinkingBonus += 0.5;
                }
            }
        }

        return (searchBonus, thinkingBonus);
    }

    private static ChatFeatures GetFeaturesForModel(string model)
    {
        return model switch
        {
            var m when ModelNames.RequiresSearch(m) => ChatFeatures.WithSearch(),
            var m when ModelNames.RequiresThinking(m) => ChatFeatures.WithThinking(),
            _ => ChatFeatures.Basic()
        };
    }

    private static UserIntent GetUserIntent(ChatFeatures features)
    {
        if (features.EnableWebSearch && features.EnableThinking)
            return UserIntent.ThinkingWithSearch;
        if (features.EnableWebSearch)
            return UserIntent.Search;
        if (features.EnableThinking)
            return UserIntent.Thinking;
        return UserIntent.Basic;
    }

    private static string[] GetSearchKeywords()
    {
        return
        [
            "搜索", "查找", "查询", "search", "find", "lookup",
            "最新", "最近", "今天", "现在", "实时", "latest", "recent", "today", "now", "real-time",
            "新闻", "消息", "动态", "news", "update", "information",
            "当前", "目前", "现状", "current", "present", "status",
            "什么时候", "何时", "when", "时间", "time",
            "天气", "股价", "价格", "weather", "stock", "price",
            "发生了什么", "what happened", "最新发展", "recent development"
        ];
    }

    private static string[] GetThinkingKeywords()
    {
        return
        [
            "分析", "解释", "说明", "analyze", "explain", "clarify",
            "为什么", "怎么", "如何", "why", "how", "what",
            "原理", "机制", "原因", "principle", "mechanism", "reason",
            "比较", "对比", "区别", "compare", "contrast", "difference",
            "优缺点", "利弊", "pros and cons", "advantages", "disadvantages",
            "思考", "推理", "逻辑", "think", "reasoning", "logic",
            "深入", "详细", "仔细", "detailed", "thorough", "careful",
            "复杂", "困难", "challenging", "complex", "difficult"
        ];
    }

    // IIntentAnalyzer 接口实现（向后兼容）
    IntentAnalysisResult IIntentAnalyzer.AnalyzeIntent(IList<Message> messages)
    {
        var features = AnalyzeIntent(messages.ToList().AsReadOnly(), "auto");
        return ConvertToIntentAnalysisResult(features);
    }

    IntentAnalysisResult IIntentAnalyzer.AnalyzeIntent(string content)
    {
        return AnalyzeSingleMessage(content);
    }

    private static IntentAnalysisResult ConvertToIntentAnalysisResult(ChatFeatures features)
    {
        var intent = GetUserIntent(features);
        return new IntentAnalysisResult(intent, features.EnableThinking, features.EnableWebSearch, "现代化分析器");
    }

    private static int CountKeywords(string content, string[] keywords)
    {
        return keywords.Count(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}

namespace OpenAI_Compatible_API_Proxy_for_Z.Services;

/// <summary>
/// 用户意图类型
/// </summary>
public enum UserIntent
{
    /// <summary>
    /// 基础对话
    /// </summary>
    Basic,
    
    /// <summary>
    /// 需要深度思考
    /// </summary>
    Thinking,
    
    /// <summary>
    /// 需要搜索信息
    /// </summary>
    Search,
    
    /// <summary>
    /// 需要思考+搜索
    /// </summary>
    ThinkingWithSearch
}

/// <summary>
/// 意图分析结果
/// </summary>
public record IntentAnalysisResult(
    UserIntent Intent,
    bool RequiresThinking,
    bool RequiresSearch,
    string Reason
);

/// <summary>
/// 用户意图分析器接口
/// </summary>
public interface IIntentAnalyzer
{
    /// <summary>
    /// 分析用户消息的意图
    /// </summary>
    /// <param name="messages">用户消息列表</param>
    /// <returns>意图分析结果</returns>
    IntentAnalysisResult AnalyzeIntent(IList<Message> messages);
    
    /// <summary>
    /// 分析单条消息的意图
    /// </summary>
    /// <param name="content">消息内容</param>
    /// <returns>意图分析结果</returns>
    IntentAnalysisResult AnalyzeIntent(string content);
}

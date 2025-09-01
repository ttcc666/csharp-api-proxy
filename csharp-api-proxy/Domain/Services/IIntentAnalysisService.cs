using OpenAI_Compatible_API_Proxy_for_Z.Domain.ValueObjects;

namespace OpenAI_Compatible_API_Proxy_for_Z.Domain.Services;

/// <summary>
/// 意图分析领域服务接口
/// </summary>
public interface IIntentAnalysisService
{
    /// <summary>
    /// 分析用户意图并确定所需功能
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="model">模型名称</param>
    /// <returns>聊天功能配置</returns>
    ChatFeatures AnalyzeIntent(IReadOnlyList<Message> messages, string model);

    /// <summary>
    /// 分析单条消息的意图
    /// </summary>
    /// <param name="content">消息内容</param>
    /// <returns>意图分析结果</returns>
    IntentAnalysisResult AnalyzeSingleMessage(string content);
}

using System.ComponentModel.DataAnnotations;
using OpenAI_Compatible_API_Proxy_for_Z.Domain.ValueObjects;

namespace OpenAI_Compatible_API_Proxy_for_Z.Domain.Entities;

/// <summary>
/// 聊天会话实体
/// </summary>
public sealed class ChatSession
{
    /// <summary>
    /// 会话ID
    /// </summary>
    [Required]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 消息ID
    /// </summary>
    [Required]
    public string MessageId { get; init; } = string.Empty;

    /// <summary>
    /// 模型名称
    /// </summary>
    [Required]
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// 消息列表
    /// </summary>
    public IReadOnlyList<Message> Messages { get; init; } = [];

    /// <summary>
    /// 功能配置
    /// </summary>
    public ChatFeatures Features { get; init; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 是否为流式请求
    /// </summary>
    public bool IsStreaming { get; init; }
}

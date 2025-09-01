using MediatR;
using System.ComponentModel.DataAnnotations;

namespace OpenAI_Compatible_API_Proxy_for_Z.Application.Commands;

/// <summary>
/// 处理聊天请求命令
/// </summary>
public sealed record ProcessChatCommand : IRequest<ProcessChatResponse>
{
    /// <summary>
    /// 请求ID（用于追踪）
    /// </summary>
    [Required]
    public string RequestId { get; init; } = string.Empty;

    /// <summary>
    /// 模型名称
    /// </summary>
    [Required]
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// 消息列表
    /// </summary>
    [Required]
    public IReadOnlyList<Message> Messages { get; init; } = [];

    /// <summary>
    /// 是否流式响应
    /// </summary>
    public bool Stream { get; init; }

    /// <summary>
    /// 温度参数
    /// </summary>
    public double? Temperature { get; init; }

    /// <summary>
    /// 最大令牌数
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// API密钥
    /// </summary>
    [Required]
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// 处理聊天请求响应
/// </summary>
public sealed record ProcessChatResponse
{
    /// <summary>
    /// 会话ID
    /// </summary>
    [Required]
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// 上游请求对象
    /// </summary>
    [Required]
    public UpstreamRequest UpstreamRequest { get; init; } = null!;

    /// <summary>
    /// 认证令牌
    /// </summary>
    [Required]
    public string AuthToken { get; init; } = string.Empty;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; init; }
}

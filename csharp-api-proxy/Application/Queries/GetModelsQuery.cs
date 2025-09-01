using MediatR;

namespace OpenAI_Compatible_API_Proxy_for_Z.Application.Queries;

/// <summary>
/// 获取可用模型列表查询
/// </summary>
public sealed record GetModelsQuery : IRequest<GetModelsResponse>
{
    /// <summary>
    /// 是否包含动态模型
    /// </summary>
    public bool IncludeDynamicModels { get; init; } = true;

    /// <summary>
    /// 取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// 获取模型列表响应
/// </summary>
public sealed record GetModelsResponse
{
    /// <summary>
    /// 模型列表
    /// </summary>
    public IReadOnlyList<ModelInfo> Models { get; init; } = [];

    /// <summary>
    /// 是否来自缓存
    /// </summary>
    public bool FromCache { get; init; }

    /// <summary>
    /// 是否有回退模型
    /// </summary>
    public bool HasFallback { get; init; }
}

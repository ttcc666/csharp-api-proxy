namespace OpenAI_Compatible_API_Proxy_for_Z.Services;

/// <summary>
/// 动态模型信息
/// </summary>
public record DynamicModelInfo(
    string Id,
    string Name,
    string Object,
    long Created,
    string OwnedBy,
    bool IsActive = true
);

/// <summary>
/// 动态模型服务接口 - 从上游API获取可用模型
/// </summary>
public interface IDynamicModelService
{
    /// <summary>
    /// 获取可用模型列表
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型列表</returns>
    Task<IList<DynamicModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 刷新模型缓存
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task RefreshModelCacheAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检查模型是否有效
    /// </summary>
    /// <param name="modelId">模型ID</param>
    /// <returns>是否有效</returns>
    Task<bool> IsValidModelAsync(string modelId);
}


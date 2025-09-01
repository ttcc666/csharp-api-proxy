namespace OpenAI_Compatible_API_Proxy_for_Z;

/// <summary>
/// 支持的模型名称常量
/// </summary>
public static class ModelNames
{
    /// <summary>
    /// 默认模型 - 标准对话
    /// </summary>
    public const string Default = "GLM-4.5";
    
    /// <summary>
    /// 思考模型 - 启用思考模式
    /// </summary>
    public const string Thinking = "GLM-4.5-Thinking";
    
    /// <summary>
    /// 搜索模型 - 启用思考模式和网络搜索
    /// </summary>
    public const string Search = "GLM-4.5-Search";
    
    /// <summary>
    /// 智能模式 - 根据用户输入自动决定功能
    /// </summary>
    public const string Auto = "GLM-4.5-Auto";
    
    /// <summary>
    /// 上游实际使用的模型ID
    /// </summary>
    public const string UpstreamModelId = "0727-360B-API";
    
    /// <summary>
    /// 搜索功能使用的 MCP 服务器
    /// </summary>
    public const string SearchMcpServer = "deep-web-search";
    
    /// <summary>
    /// 检查是否为有效的模型名称
    /// </summary>
    /// <param name="modelName">模型名称</param>
    /// <returns>是否为有效模型</returns>
    public static bool IsValidModel(string modelName)
    {
        return modelName == Default || modelName == Thinking || modelName == Search || modelName == Auto;
    }
    
    /// <summary>
    /// 检查模型是否需要智能调度
    /// </summary>
    /// <param name="modelName">模型名称</param>
    /// <returns>是否需要智能调度</returns>
    public static bool RequiresSmartDispatch(string modelName)
    {
        return modelName == Default || modelName == Auto;
    }
    
    /// <summary>
    /// 检查模型是否需要启用思考功能
    /// </summary>
    /// <param name="modelName">模型名称</param>
    /// <returns>是否需要思考功能</returns>
    public static bool RequiresThinking(string modelName)
    {
        return modelName == Thinking || modelName == Search;
    }
    
    /// <summary>
    /// 检查模型是否需要启用搜索功能
    /// </summary>
    /// <param name="modelName">模型名称</param>
    /// <returns>是否需要搜索功能</returns>
    public static bool RequiresSearch(string modelName)
    {
        return modelName == Search;
    }
}

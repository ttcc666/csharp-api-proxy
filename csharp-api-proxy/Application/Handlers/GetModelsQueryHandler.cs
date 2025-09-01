using MediatR;
using OpenAI_Compatible_API_Proxy_for_Z.Application.Queries;
using OpenAI_Compatible_API_Proxy_for_Z.Services;
using System.Diagnostics;

namespace OpenAI_Compatible_API_Proxy_for_Z.Application.Handlers;

/// <summary>
/// 获取模型列表查询处理器
/// </summary>
public sealed class GetModelsQueryHandler : IRequestHandler<GetModelsQuery, GetModelsResponse>
{
    private readonly IDynamicModelService _dynamicModelService;
    private readonly ILogger<GetModelsQueryHandler> _logger;

    private static readonly ActivitySource ActivitySource = new("OpenAI.Proxy.QueryHandler");

    public GetModelsQueryHandler(
        IDynamicModelService dynamicModelService,
        ILogger<GetModelsQueryHandler> logger)
    {
        _dynamicModelService = dynamicModelService;
        _logger = logger;
    }

    public async Task<GetModelsResponse> Handle(GetModelsQuery request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("GetModelsQuery");
        activity?.SetTag("include_dynamic", request.IncludeDynamicModels);

        try
        {
            if (request.IncludeDynamicModels)
            {
                try
                {
                    var dynamicModels = await _dynamicModelService.GetAvailableModelsAsync();
                    var modelInfos = dynamicModels.Select(m => new ModelInfo(
                        Id: m.Id,
                        Object: m.Object,
                        Created: m.Created,
                        OwnedBy: m.OwnedBy
                    )).ToList();

                    _logger.LogInformation("成功获取 {Count} 个动态模型", modelInfos.Count);
                    
                    return new GetModelsResponse
                    {
                        Models = modelInfos,
                        FromCache = false,
                        HasFallback = false
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "获取动态模型失败，使用静态模型列表");
                    // 继续使用静态模型列表
                }
            }

            // 使用静态模型列表
            var fallbackModels = GetStaticModels();
            
            _logger.LogInformation("使用静态模型列表，共 {Count} 个模型", fallbackModels.Count);
            
            return new GetModelsResponse
            {
                Models = fallbackModels,
                FromCache = false,
                HasFallback = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取模型列表失败");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            // 最终回退到基础模型列表
            var basicModels = GetStaticModels();
            return new GetModelsResponse
            {
                Models = basicModels,
                FromCache = false,
                HasFallback = true
            };
        }
    }

    private static List<ModelInfo> GetStaticModels()
    {
        var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
        
        return
        [
            new ModelInfo(ModelNames.Default, "model", timestamp, "z.ai"),
            new ModelInfo(ModelNames.Thinking, "model", timestamp, "z.ai"),
            new ModelInfo(ModelNames.Search, "model", timestamp, "z.ai"),
            new ModelInfo(ModelNames.Auto, "model", timestamp, "z.ai")
        ];
    }
}

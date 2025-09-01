using MediatR;
using Microsoft.Extensions.Options;
using OpenAI_Compatible_API_Proxy_for_Z.Application.Commands;
using OpenAI_Compatible_API_Proxy_for_Z.Domain.Services;
using OpenAI_Compatible_API_Proxy_for_Z.Domain.Entities;
using OpenAI_Compatible_API_Proxy_for_Z.Services;
using System.Diagnostics;

namespace OpenAI_Compatible_API_Proxy_for_Z.Application.Handlers;

/// <summary>
/// 处理聊天请求命令处理器
/// </summary>
public sealed class ProcessChatCommandHandler : IRequestHandler<ProcessChatCommand, ProcessChatResponse>
{
    private readonly IIntentAnalysisService _intentAnalysisService;
    private readonly IUpstreamService _upstreamService;
    private readonly ProxySettings _settings;
    private readonly ILogger<ProcessChatCommandHandler> _logger;

    private static readonly ActivitySource ActivitySource = new("OpenAI.Proxy.CommandHandler");

    public ProcessChatCommandHandler(
        IIntentAnalysisService intentAnalysisService,
        IUpstreamService upstreamService,
        IOptions<ProxySettings> settings,
        ILogger<ProcessChatCommandHandler> logger)
    {
        _intentAnalysisService = intentAnalysisService;
        _upstreamService = upstreamService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ProcessChatResponse> Handle(ProcessChatCommand request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("ProcessChatCommand");
        activity?.SetTag("request.id", request.RequestId);
        activity?.SetTag("model", request.Model);
        activity?.SetTag("stream", request.Stream);

        try
        {
            // 验证API密钥
            if (request.ApiKey != _settings.DefaultKey)
            {
                _logger.LogWarning("无效的API密钥 [RequestId: {RequestId}]", request.RequestId);
                return new ProcessChatResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "无效的API密钥"
                };
            }

            // 验证模型
            if (!ModelNames.IsValidModel(request.Model))
            {
                _logger.LogWarning("不支持的模型: {Model} [RequestId: {RequestId}]", request.Model, request.RequestId);
                return new ProcessChatResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"不支持的模型: {request.Model}"
                };
            }

            // 创建聊天会话
            var chatSession = new ChatSession
            {
                Id = GenerateSessionId(),
                MessageId = GenerateMessageId(),
                Model = request.Model,
                Messages = request.Messages,
                Features = _intentAnalysisService.AnalyzeIntent(request.Messages, request.Model),
                IsStreaming = request.Stream
            };

            _logger.LogInformation("创建聊天会话 [RequestId: {RequestId}] [SessionId: {SessionId}] [Features: Thinking={Thinking}, Search={Search}]",
                request.RequestId, chatSession.Id, chatSession.Features.EnableThinking, chatSession.Features.EnableWebSearch);

            // 构建上游请求
            var upstreamRequest = BuildUpstreamRequest(chatSession);

            // 获取认证令牌
            var authToken = await _upstreamService.GetAuthTokenAsync(cancellationToken);

            return new ProcessChatResponse
            {
                SessionId = chatSession.Id,
                UpstreamRequest = upstreamRequest,
                AuthToken = authToken,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理聊天命令失败 [RequestId: {RequestId}]", request.RequestId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            return new ProcessChatResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private UpstreamRequest BuildUpstreamRequest(ChatSession session)
    {
        return new UpstreamRequest(
            Stream: true,
            ChatId: session.Id,
            Id: session.MessageId,
            Model: ModelNames.UpstreamModelId,
            Messages: session.Messages.ToList(),
            Params: new Dictionary<string, object>(),
            Features: session.Features.ToFeaturesDictionary(),
            BackgroundTasks: new Dictionary<string, bool> 
            { 
                { "title_generation", false }, 
                { "tags_generation", false } 
            },
            McpServers: session.Features.McpServers.ToList(),
            ModelItem: new ModelItem(ModelNames.UpstreamModelId, "GLM-4.5", "openai"),
            ToolServers: [],
            Variables: new Dictionary<string, string>
            {
                { "{{USER_NAME}}", "User" },
                { "{{USER_LOCATION}}", "Unknown" },
                { "{{CURRENT_DATETIME}}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            }
        );
    }

    private static string GenerateSessionId()
    {
        var timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var seconds = DateTimeOffset.Now.ToUnixTimeSeconds();
        return $"{timestamp}-{seconds}";
    }

    private static string GenerateMessageId()
    {
        return DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
    }
}

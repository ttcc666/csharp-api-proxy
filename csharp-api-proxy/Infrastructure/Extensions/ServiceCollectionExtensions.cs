using FluentValidation;
using MediatR;
using Microsoft.Extensions.ObjectPool;
using OpenAI_Compatible_API_Proxy_for_Z.Application.Handlers;
using OpenAI_Compatible_API_Proxy_for_Z.Configuration;
using OpenAI_Compatible_API_Proxy_for_Z.Domain.Services;
using OpenAI_Compatible_API_Proxy_for_Z.Infrastructure.Services;
using OpenAI_Compatible_API_Proxy_for_Z.Infrastructure.Streaming;
using OpenAI_Compatible_API_Proxy_for_Z.Services;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using System.Text;
using System.Text.Json;
using LogDashboard;

namespace OpenAI_Compatible_API_Proxy_for_Z.Infrastructure.Extensions;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加应用程序核心服务
    /// </summary>
    public static IServiceCollection AddApplicationCore(this IServiceCollection services)
    {
        // 添加MediatR
        services.AddMediatR(typeof(ProcessChatCommandHandler).Assembly);

        // 添加验证器
        services.AddValidatorsFromAssembly(typeof(ProcessChatCommandHandler).Assembly);

        return services;
    }

    /// <summary>
    /// 添加配置服务
    /// </summary>
    public static IServiceCollection AddConfigurationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 强类型配置
        services.Configure<ProxySettings>(configuration.GetSection("ProxySettings"));
        services.Configure<IntentAnalysisSettings>(configuration.GetSection("IntentAnalysisSettings"));
        services.Configure<ToolSettings>(configuration.GetSection("ToolSettings"));

        // 配置验证
        services.AddOptions<ProxySettings>()
            .Bind(configuration.GetSection("ProxySettings"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<IntentAnalysisSettings>()
            .Bind(configuration.GetSection("IntentAnalysisSettings"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// 添加HTTP客户端服务
    /// </summary>
    public static IServiceCollection AddHttpClientServices(this IServiceCollection services)
    {
        // 配置重试策略
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // 在重试回调中记录日志
                    Console.WriteLine($"HTTP请求重试 [Attempt: {retryCount}] [Delay: {timespan.TotalMilliseconds}ms]");
                });

        // 配置超时策略
        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));

        // 配置HttpClient
        services.AddHttpClient<IUpstreamService, UpstreamService>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProxySettings>>().Value;
            client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .AddPolicyHandler(retryPolicy)
        .AddPolicyHandler(timeoutPolicy);

        return services;
    }

    /// <summary>
    /// 添加业务服务
    /// </summary>
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        // 注册领域服务
        services.AddScoped<IIntentAnalysisService, IntentAnalysisService>();

        // 注册基础设施服务
        services.AddScoped<IUpstreamService, UpstreamService>();
        services.AddScoped<IContentTransformer, ContentTransformer>();
        services.AddScoped<IOpenAIResponseBuilder, OpenAIResponseBuilder>();
        services.AddScoped<IDynamicModelService, DynamicModelService>();

        // 注册新的意图分析器（使用现代化的Infrastructure版本）
        services.AddScoped<IIntentAnalyzer, IntentAnalysisService>();

        // 注册高性能组件
        services.AddScoped<HighPerformanceStreamProcessor>();
        services.AddScoped<OpenAI_Compatible_API_Proxy_for_Z.Infrastructure.Handlers.ApiEndpointHandler>();

        // 注册内容转换器（使用默认实现）
        // 注意：已移除StatefulContentTransformer和旧的Handler，统一使用ContentTransformer

        return services;
    }

    /// <summary>
    /// 添加性能优化服务
    /// </summary>
    public static IServiceCollection AddPerformanceServices(this IServiceCollection services)
    {
        // 内存缓存
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1000;
            options.CompactionPercentage = 0.1;
        });

        // 对象池
        services.AddSingleton<ObjectPool<StringBuilder>>(serviceProvider =>
        {
            var provider = new DefaultObjectPoolProvider();
            return provider.CreateStringBuilderPool();
        });

        // JSON序列化配置
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.SerializerOptions.WriteIndented = false;
            options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

        return services;
    }

    /// <summary>
    /// 添加监控和可观测性服务
    /// </summary>
    public static IServiceCollection AddObservabilityServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 配置Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "OpenAI-Proxy")
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();

        services.AddSerilog();

        // 健康检查
        services.AddHealthChecks()
            .AddCheck<OpenAI_Compatible_API_Proxy_for_Z.HealthChecks.UpstreamHealthCheck>("upstream");

        return services;
    }

    /// <summary>
    /// 添加API文档服务
    /// </summary>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services, IWebHostEnvironment environment)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();
        
        // 添加 LogDashboard
        services.AddLogDashboard();

        if (environment.IsDevelopment())
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "OpenAI Compatible API Proxy for Z.AI",
                    Version = "v2.0",
                    Description = "现代化的OpenAI API兼容代理服务，具有高性能、可观测性和智能调度功能",
                    Contact = new Microsoft.OpenApi.Models.OpenApiContact
                    {
                        Name = "API 支持",
                        Email = "support@example.com"
                    }
                });

                // 添加认证配置
                c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme",
                    Name = "Authorization",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                // 包含XML注释
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }
            });
        }

        return services;
    }

    /// <summary>
    /// 添加CORS服务
    /// </summary>
    public static IServiceCollection AddCorsServices(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });

            // 开发环境的宽松策略
            options.AddPolicy("Development", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .WithExposedHeaders("X-Request-ID");
            });

            // 生产环境的严格策略
            options.AddPolicy("Production", policy =>
            {
                policy.WithOrigins("https://chat.z.ai", "https://api.openai.com")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .WithExposedHeaders("X-Request-ID")
                      .AllowCredentials();
            });
        });

        return services;
    }
}

using OpenAI_Compatible_API_Proxy_for_Z.Infrastructure.Extensions;
using OpenAI_Compatible_API_Proxy_for_Z.Middleware;
using Serilog;
using LogDashboard;

// 配置 Serilog 作为启动日志记录器
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("启动 OpenAI Compatible API Proxy for Z.AI v2.0");

    var builder = WebApplication.CreateBuilder(args);

    // 添加 Serilog
    builder.Host.UseSerilog();

    // 模块化服务注册
    builder.Services
        .AddApplicationCore()                                    // MediatR + 验证
        .AddConfigurationServices(builder.Configuration)        // 强类型配置
        .AddHttpClientServices()                                // HTTP客户端 + 重试策略
        .AddBusinessServices()                                  // 业务服务
        .AddPerformanceServices()                               // 性能优化
        .AddObservabilityServices(builder.Configuration)        // 监控和日志
        .AddApiDocumentation(builder.Environment)               // API文档
        .AddCorsServices();                                     // CORS配置

    var app = builder.Build();

    // 配置中间件管道
    app.UseMiddleware<RequestLoggingMiddleware>();      // 详细请求日志
    app.UseMiddleware<PerformanceMonitoringMiddleware>(); // 性能监控
    app.UseMiddleware<GlobalExceptionMiddleware>();     // 全局异常处理

    // 开发环境配置
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenAI Compatible API Proxy v2.0");
            c.RoutePrefix = "docs";
            c.DisplayRequestDuration();
            c.EnableDeepLinking();
        });
        app.UseCors("Development");
    }
    else
    {
        app.UseCors("Production");
    }

    app.UseHttpsRedirection();

    // 健康检查端点
    app.MapHealthChecks("/health").WithTags("Health");

    // 配置 LogDashboard
    app.UseLogDashboard();

    // 配置API端点
    app.ConfigureApiEndpoints();

    Log.Information("应用程序启动完成，正在监听请求...");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用程序启动失败");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}


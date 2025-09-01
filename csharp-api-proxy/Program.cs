using Microsoft.Extensions.Options;
using OpenAI_Compatible_API_Proxy_for_Z;
using OpenAI_Compatible_API_Proxy_for_Z.Middleware;
using OpenAI_Compatible_API_Proxy_for_Z.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<ProxySettings>(builder.Configuration.GetSection("ProxySettings"));

// Configure HttpClient with timeout and other settings
builder.Services.AddHttpClient<IUpstreamService, UpstreamService>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<ProxySettings>>().Value;
    client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Add memory cache for token caching
builder.Services.AddMemoryCache();

// Register services
builder.Services.AddScoped<ChatHandler>();
builder.Services.AddScoped<IUpstreamService, UpstreamService>();
builder.Services.AddScoped<IContentTransformer, ContentTransformer>();
builder.Services.AddScoped<IOpenAIResponseBuilder, OpenAIResponseBuilder>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<OpenAI_Compatible_API_Proxy_for_Z.HealthChecks.UpstreamHealthCheck>("upstream");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Configure JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.WriteIndented = false;
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 添加 Swagger 配置用于更好的API文档
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "OpenAI Compatible API Proxy for Z.AI",
            Version = "v1.0",
            Description = "一个与 OpenAI API 兼容的代理服务，用于转发请求到 Z.AI 服务",
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
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenAI Compatible API Proxy v1");
        c.RoutePrefix = "docs";
    });
}

app.UseCors();
app.UseHttpsRedirection();

// Add health check endpoint
app.MapHealthChecks("/health");

app.MapGet("/v1/models", (IOptions<ProxySettings> settings) =>
{
    var response = new ModelsResponse(
        Object: "list",
        Data: new List<ModelInfo>
        {
            new ModelInfo(
                Id: settings.Value.ModelName,
                Object: "model",
                Created: DateTimeOffset.Now.ToUnixTimeSeconds(),
                OwnedBy: "z.ai"
            )
        }
    );
    return Results.Ok(response);
});

app.MapMethods("/v1/chat/completions", new[] { "POST", "OPTIONS" }, async (HttpContext context, ChatHandler handler) => await handler.Handle(context));

app.Run();
# OpenAI Compatible API Proxy for Z.AI

一个与 OpenAI API 兼容的代理服务，用于转发请求到 Z.AI 服务。

## 🚀 主要功能

- **OpenAI API 兼容**: 完全兼容 OpenAI 的聊天完成 API
- **流式响应支持**: 支持实时流式响应
- **思维内容转换**: 自动处理和转换 Z.AI 的思维内容
- **全局异常处理**: 统一的错误处理和响应格式
- **健康检查**: 内置的健康检查端点
- **缓存机制**: 令牌缓存优化性能
- **配置验证**: 启动时验证配置项

## 🛠️ 技术栈

- **.NET 9.0**: 最新的 .NET 框架
- **ASP.NET Core**: 高性能 Web API 框架
- **依赖注入**: 使用内置 DI 容器
- **内存缓存**: 用于令牌缓存
- **Swagger/OpenAPI**: API 文档生成

## 📦 安装和配置

### 1. 克隆项目

```bash
git clone <repository-url>
cd csharp-api-proxy
```

### 2. 配置应用

编辑 `appsettings.json` 文件，设置您的 API 密钥和上游令牌：

```json
{
  "ProxySettings": {
    "DefaultKey": "sk-your-api-key-here",
    "UpstreamToken": "your-upstream-token-here"
  }
}
```

这就是最基本的配置！其他配置项都有默认值。

> 📋 查看 `配置说明.md` 了解所有配置选项的详细说明。

### 3. 安装依赖

```bash
dotnet restore
```

### 4. 运行应用

```bash
dotnet run
```

## 🔧 配置选项

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `UpstreamUrl` | 上游 Z.AI API 地址 | `https://chat.z.ai/api/chat/completions` |
| `DefaultKey` | API 密钥 | 需要配置 |
| `UpstreamToken` | 上游服务令牌 | 需要配置 |
| `ModelName` | 模型名称 | `GLM-4.5` |
| `ThinkTagsMode` | 思维标签处理模式 | `strip` |
| `AnonTokenEnabled` | 是否启用匿名令牌 | `true` |
| `RequestTimeoutSeconds` | 请求超时时间（秒） | `120` |
| `TokenCacheMinutes` | 令牌缓存时间（分钟） | `30` |

### 思维标签处理模式

- `strip`: 移除所有思维标签
- `think`: 将 `<details>` 标签转换为 `<think>` 标签
- `keep`: 保持原样

## 🚀 API 端点

### 聊天完成

```http
POST /v1/chat/completions
Content-Type: application/json
Authorization: Bearer your-api-key

{
  "model": "GLM-4.5",
  "messages": [
    {"role": "user", "content": "Hello!"}
  ],
  "stream": false,
  "temperature": 0.7,
  "max_tokens": 1000
}
```

### 模型列表

```http
GET /v1/models
```

### 健康检查

```http
GET /health
```

## 📚 API 文档

在开发环境中，访问以下地址查看 API 文档：

- Swagger UI: `http://localhost:5000/docs`
- OpenAPI 规范: `http://localhost:5000/swagger/v1/swagger.json`

## 🏗️ 架构设计

项目采用分层架构，主要组件包括：

- **ChatHandler**: 处理聊天请求的主要控制器
- **IUpstreamService**: 上游服务接口，处理 Z.AI API 调用
- **IContentTransformer**: 内容转换服务，处理思维内容转换
- **IOpenAIResponseBuilder**: OpenAI 响应构建器
- **GlobalExceptionMiddleware**: 全局异常处理中间件
- **UpstreamHealthCheck**: 上游服务健康检查

## 🔐 安全性

- **API 密钥验证**: 所有请求都需要有效的 Bearer 令牌
- **配置验证**: 启动时验证所有必需的配置项
- **敏感信息保护**: 敏感配置通过用户机密或环境变量管理
- **CORS 配置**: 可配置的跨域资源共享策略

## 🔍 监控和日志

- **结构化日志**: 使用结构化日志记录关键操作
- **健康检查**: 监控上游服务状态
- **错误处理**: 统一的错误响应格式
- **性能监控**: HTTP 客户端超时和缓存机制

## 🚀 部署

### Docker 部署

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["csharp-api-proxy.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "csharp-api-proxy.dll"]
```

### 环境变量

```bash
ProxySettings__DefaultKey=your-api-key
ProxySettings__UpstreamToken=your-token
ProxySettings__DebugMode=false
```

## 🤝 贡献

欢迎提交 Issue 和 Pull Request 来改进这个项目。

## 📄 许可证

[MIT License](LICENSE)

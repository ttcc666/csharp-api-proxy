# 使用官方的 .NET 9.0 运行时作为基础镜像
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# 使用 SDK 镜像进行构建
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 复制项目文件并还原依赖
COPY ["csharp-api-proxy.csproj", "csharp-api-proxy/"]
RUN dotnet restore "./csharp-api-proxy/csharp-api-proxy.csproj"

# 复制所有源码并构建
COPY . ./csharp-api-proxy/
WORKDIR "/src/csharp-api-proxy"
RUN dotnet build "./csharp-api-proxy.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 发布应用
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./csharp-api-proxy.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 最终运行时镜像
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# 创建非 root 用户
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "csharp-api-proxy.dll"]

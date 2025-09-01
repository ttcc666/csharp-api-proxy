using Microsoft.Extensions.ObjectPool;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Buffers;
using OpenAI_Compatible_API_Proxy_for_Z.Services;

namespace OpenAI_Compatible_API_Proxy_for_Z.Infrastructure.Streaming;

/// <summary>
/// 高性能流处理器
/// </summary>
public sealed class HighPerformanceStreamProcessor : IDisposable
{
    private readonly IContentTransformer _contentTransformer;
    private readonly IOpenAIResponseBuilder _responseBuilder;
    private readonly ObjectPool<StringBuilder> _stringBuilderPool;
    private readonly ILogger<HighPerformanceStreamProcessor> _logger;

    // 使用Channel来处理背压
    private readonly Channel<ProcessedChunk> _processingChannel;
    private readonly ChannelWriter<ProcessedChunk> _writer;
    private readonly ChannelReader<ProcessedChunk> _reader;

    // 性能统计
    private long _processedChunks;
    private long _processedBytes;
    private readonly object _statsLock = new();

    public HighPerformanceStreamProcessor(
        IContentTransformer contentTransformer,
        IOpenAIResponseBuilder responseBuilder,
        ObjectPool<StringBuilder> stringBuilderPool,
        ILogger<HighPerformanceStreamProcessor> logger)
    {
        _contentTransformer = contentTransformer;
        _responseBuilder = responseBuilder;
        _stringBuilderPool = stringBuilderPool;
        _logger = logger;

        // 创建有界通道以实现背压控制
        var channelOptions = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false
        };

        _processingChannel = Channel.CreateBounded<ProcessedChunk>(channelOptions);
        _writer = _processingChannel.Writer;
        _reader = _processingChannel.Reader;
    }

    /// <summary>
    /// 处理流式响应（使用Pipeline进行高性能处理）
    /// </summary>
    public async Task ProcessStreamAsync(
        Stream responseStream,
        Func<string, Task> writeChunkAsync,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var pipe = new Pipe();
        var writer = pipe.Writer;
        var reader = pipe.Reader;

        // 启动生产者任务
        var producerTask = ProduceAsync(responseStream, writer, requestId, cancellationToken);

        // 启动消费者任务
        var consumerTask = ConsumeAsync(reader, writeChunkAsync, requestId, cancellationToken);

        // 等待两个任务完成
        await Task.WhenAll(producerTask, consumerTask);

        LogPerformanceStats(requestId);
    }

    /// <summary>
    /// 生产者：读取原始数据流并写入Pipeline
    /// </summary>
    private async Task ProduceAsync(
        Stream source,
        PipeWriter writer,
        string requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[4096];
            
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await source.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0) break;

                // 获取内存并写入数据
                var memory = writer.GetMemory(bytesRead);
                buffer.AsMemory(0, bytesRead).CopyTo(memory);
                writer.Advance(bytesRead);

                // 通知读取器有数据可用
                var flushResult = await writer.FlushAsync(cancellationToken);
                if (flushResult.IsCompleted) break;

                // 更新统计
                Interlocked.Add(ref _processedBytes, bytesRead);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生产者任务失败 [RequestId: {RequestId}]", requestId);
            throw;
        }
        finally
        {
            await writer.CompleteAsync();
        }
    }

    /// <summary>
    /// 消费者：从Pipeline读取数据并处理
    /// </summary>
    private async Task ConsumeAsync(
        PipeReader reader,
        Func<string, Task> writeChunkAsync,
        string requestId,
        CancellationToken cancellationToken)
    {
        var stringBuilder = _stringBuilderPool.Get();
        var firstChunkSent = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var readResult = await reader.ReadAsync(cancellationToken);
                var buffer = readResult.Buffer;

                if (buffer.IsEmpty && readResult.IsCompleted) break;

                // 处理缓冲区中的数据
                firstChunkSent = await ProcessBufferAsync(buffer, stringBuilder, writeChunkAsync, 
                    requestId, firstChunkSent, cancellationToken);

                // 标记已处理的数据
                reader.AdvanceTo(buffer.End);

                if (readResult.IsCompleted) break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "消费者任务失败 [RequestId: {RequestId}]", requestId);
            throw;
        }
        finally
        {
            await reader.CompleteAsync();
            _stringBuilderPool.Return(stringBuilder);
        }
    }

    /// <summary>
    /// 处理缓冲区数据
    /// </summary>
    private async Task<bool> ProcessBufferAsync(
        ReadOnlySequence<byte> buffer,
        StringBuilder lineBuilder,
        Func<string, Task> writeChunkAsync,
        string requestId,
        bool firstChunkSent,
        CancellationToken cancellationToken)
    {
        // 发送第一个chunk
        if (!firstChunkSent)
        {
            var firstChunk = _responseBuilder.BuildStreamChunk(null, null, isFirst: true);
            await writeChunkAsync(JsonSerializer.Serialize(firstChunk));
            firstChunkSent = true;
        }

        // 将缓冲区转换为字符串进行处理
        var text = Encoding.UTF8.GetString(buffer.ToArray());
        
        foreach (var c in text)
        {
            if (c == '\n')
            {
                // 处理完整的行
                var line = lineBuilder.ToString();
                lineBuilder.Clear();
                
                await ProcessLineAsync(line, writeChunkAsync, requestId, cancellationToken);
            }
            else if (c != '\r')
            {
                lineBuilder.Append(c);
            }
        }
        
        return firstChunkSent;
    }

    /// <summary>
    /// 处理单行数据
    /// </summary>
    private async Task ProcessLineAsync(
        string line,
        Func<string, Task> writeChunkAsync,
        string requestId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) 
            return;

        var dataStr = line.Substring("data: ".Length);
        if (string.IsNullOrEmpty(dataStr)) 
            return;

        try
        {
            var upstreamData = _contentTransformer.DeserializeUpstreamData(dataStr);
            if (upstreamData == null) 
                return;

            // 检查错误
            if (HasError(upstreamData))
            {
                _logger.LogWarning("检测到上游错误 [RequestId: {RequestId}]", requestId);
                var errorChunk = _responseBuilder.BuildStreamChunk("抱歉，上游服务返回错误，请稍后重试。", null);
                await writeChunkAsync(JsonSerializer.Serialize(errorChunk));
                await writeChunkAsync("[DONE]");
                return;
            }

            // 处理内容
            if (!string.IsNullOrEmpty(upstreamData.Data.DeltaContent))
            {
                var (content, reasoningContent) = _contentTransformer.ProcessContent(
                    upstreamData.Data.DeltaContent, upstreamData.Data.Phase);

                if (!string.IsNullOrEmpty(content) || !string.IsNullOrEmpty(reasoningContent))
                {
                    var chunk = _responseBuilder.BuildStreamChunk(content, reasoningContent);
                    await writeChunkAsync(JsonSerializer.Serialize(chunk));
                    
                    Interlocked.Increment(ref _processedChunks);
                }
            }

            // 检查是否完成
            if (upstreamData.Data.Done || upstreamData.Data.Phase == "done")
            {
                var endChunk = _responseBuilder.BuildStreamChunk(null, null, isLast: true);
                await writeChunkAsync(JsonSerializer.Serialize(endChunk));
                await writeChunkAsync("[DONE]");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON解析失败，跳过此行 [RequestId: {RequestId}] [Data: {Data}]", 
                requestId, dataStr.Length > 100 ? dataStr[..100] + "..." : dataStr);
        }
    }

    /// <summary>
    /// 检查是否有错误
    /// </summary>
    private static bool HasError(UpstreamData upstreamData)
    {
        return upstreamData.Error != null ||
               upstreamData.Data.Error != null ||
               upstreamData.Data.Inner?.Error != null;
    }

    /// <summary>
    /// 记录性能统计
    /// </summary>
    private void LogPerformanceStats(string requestId)
    {
        lock (_statsLock)
        {
            _logger.LogInformation(
                "流处理完成 [RequestId: {RequestId}] [Chunks: {Chunks}] [Bytes: {Bytes}]",
                requestId, _processedChunks, _processedBytes);
        }
    }

    public void Dispose()
    {
        _writer.TryComplete();
    }
}

/// <summary>
/// 已处理的数据块
/// </summary>
internal sealed record ProcessedChunk(string Content, bool IsLast = false);

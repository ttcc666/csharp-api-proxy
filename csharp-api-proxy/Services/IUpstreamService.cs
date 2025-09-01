namespace OpenAI_Compatible_API_Proxy_for_Z.Services;

public interface IUpstreamService
{
    Task<string> GetAuthTokenAsync(CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> CallApiAsync(UpstreamRequest request, string token, CancellationToken cancellationToken = default);
}

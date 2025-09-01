namespace OpenAI_Compatible_API_Proxy_for_Z.Services;

public interface IContentTransformer
{
    string TransformThinking(string content, string phase);
    UpstreamData? DeserializeUpstreamData(string dataStr);
}

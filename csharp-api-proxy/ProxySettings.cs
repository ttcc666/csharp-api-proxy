using System.ComponentModel.DataAnnotations;

namespace OpenAI_Compatible_API_Proxy_for_Z;

public class ProxySettings
{
    [Required, Url]
    public string UpstreamUrl { get; set; } = "";
    
    [Required, MinLength(6)]
    public string DefaultKey { get; set; } = "";
    
    [Required]
    public string UpstreamToken { get; set; } = "";
    
    [Required]
    public string ModelName { get; set; } = "";
    
    [AllowedValues("strip", "think", "keep")]
    public string ThinkTagsMode { get; set; } = "strip";
    
    [Required]
    public string XFeVersion { get; set; } = "";
    
    [Required]
    public string BrowserUa { get; set; } = "";
    
    [Required]
    public string SecChUa { get; set; } = "";
    
    [Required]
    public string SecChUaMob { get; set; } = "";
    
    [Required]
    public string SecChUaPlat { get; set; } = "";
    
    [Required, Url]
    public string OriginBase { get; set; } = "";
    
    public bool AnonTokenEnabled { get; set; }
    public bool DebugMode { get; set; }
    
    [Range(30, 300)]
    public int RequestTimeoutSeconds { get; set; } = 120;
    
    [Range(1, 60)]
    public int TokenCacheMinutes { get; set; } = 30;
}
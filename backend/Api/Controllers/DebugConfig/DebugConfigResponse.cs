namespace NzbWebDAV.Api.Controllers.DebugConfig;

public class DebugConfigResponse : BaseApiResponse
{
    public Dictionary<string, string?> Config { get; set; } = new();
    public int ProviderCount { get; set; }
    public List<Dictionary<string, string>> AllProviders { get; set; } = new();
}
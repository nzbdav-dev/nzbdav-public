namespace NzbWebDAV.Api.Controllers.CheckDatabase;

public class CheckDatabaseResponse : BaseApiResponse
{
    public int TotalConfigItems { get; set; }
    public Dictionary<string, string> LegacyUsenetConfig { get; set; } = new();
    public Dictionary<string, string> ProviderMetaConfig { get; set; } = new();
    public Dictionary<string, string> ProviderSpecificConfig { get; set; } = new();
    public Dictionary<string, string> AllConfigItems { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
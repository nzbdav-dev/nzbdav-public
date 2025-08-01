namespace NzbWebDAV.Api.Controllers.TriggerMigration;

public class TriggerMigrationResponse : BaseApiResponse
{
    public bool MigrationSuccessful { get; set; }
    public string Message { get; set; } = "";
    public int ProviderCount { get; set; }
}
namespace NzbWebDAV.Api.Controllers.GetConnectionStats;

public class GetConnectionStatsResponse : BaseApiResponse
{
    public object[] ProviderStats { get; set; } = Array.Empty<object>();
    public int TotalActiveConnections { get; set; }
    public int TotalMaxConnections { get; set; }
}
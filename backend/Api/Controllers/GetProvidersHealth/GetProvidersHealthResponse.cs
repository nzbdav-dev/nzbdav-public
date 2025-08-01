namespace NzbWebDAV.Api.Controllers.GetProvidersHealth;

public class GetProvidersHealthResponse : BaseApiResponse
{
    public object[] Providers { get; set; } = Array.Empty<object>();
}
namespace NzbWebDAV.Api.Controllers.GetUsenetProviders;

public class GetUsenetProvidersResponse : BaseApiResponse
{
    public object[] Providers { get; set; } = Array.Empty<object>();
    public int PrimaryProviderIndex { get; set; }
}
namespace NzbWebDAV.Api.Controllers.TestUsenetProvider;

public class TestUsenetProviderResponse : BaseApiResponse
{
    public bool Connected { get; set; }
    public string? ErrorMessage { get; set; }
}
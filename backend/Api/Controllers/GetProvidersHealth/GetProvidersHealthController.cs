using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Clients;

namespace NzbWebDAV.Api.Controllers.GetProvidersHealth;

[ApiController]
[Route("api/usenet-providers/health")]
public class GetProvidersHealthController(UsenetProviderManager providerManager) : BaseApiController
{
    protected override Task<IActionResult> HandleRequest()
    {
        var providersHealth = providerManager.Providers.Select(p => new
        {
            Id = p.ProviderId,
            Name = p.ProviderName,
            IsHealthy = p.IsHealthy,
            Priority = p.Priority
        }).Cast<object>().ToArray();

        var response = new GetProvidersHealthResponse
        {
            Status = true,
            Providers = providersHealth
        };

        return Task.FromResult<IActionResult>(Ok(response));
    }
}
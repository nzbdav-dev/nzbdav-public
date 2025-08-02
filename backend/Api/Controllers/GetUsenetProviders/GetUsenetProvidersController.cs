using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Clients;
using NzbWebDAV.Config;

namespace NzbWebDAV.Api.Controllers.GetUsenetProviders;

[ApiController]
[Route("api/usenet-providers")]
public class GetUsenetProvidersController(ConfigManager configManager, UsenetProviderManager providerManager) : BaseApiController
{
    protected override Task<IActionResult> HandleRequest()
    {
        var providers = providerManager.Providers.Select(p => new
        {
            Id = p.ProviderId,
            Name = p.ProviderName,
            Priority = p.Priority,
            IsHealthy = p.IsHealthy
        }).Cast<object>().ToArray();

        var response = new GetUsenetProvidersResponse
        {
            Status = true,
            Providers = providers,
            PrimaryProviderIndex = configManager.GetPrimaryProviderIndex()
        };

        return Task.FromResult<IActionResult>(Ok(response));
    }
}
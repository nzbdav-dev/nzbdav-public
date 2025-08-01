using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Clients;

namespace NzbWebDAV.Api.Controllers.TestProviders;

[ApiController]
[Route("api/test-providers")]
public class TestProvidersController : ControllerBase
{
    private readonly UsenetProviderManager _providerManager;

    public TestProvidersController(UsenetProviderManager providerManager)
    {
        _providerManager = providerManager;
    }

    [HttpGet]
    public async Task<IActionResult> TestProviders()
    {
        try
        {
            var results = new List<object>();
            var providers = _providerManager.GetAllProviders();

            foreach (var provider in providers)
            {
                var startTime = DateTime.UtcNow;
                var success = await provider.TestConnectionAsync();
                var duration = DateTime.UtcNow - startTime;

                results.Add(new
                {
                    ProviderId = provider.ProviderId,
                    ProviderName = provider.ProviderName,
                    Host = provider.Host,
                    Port = provider.Port,
                    Priority = provider.Priority,
                    IsHealthy = provider.IsHealthy,
                    ConnectionTest = success,
                    TestDurationMs = (int)duration.TotalMilliseconds
                });
            }

            return Ok(new { providers = results });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
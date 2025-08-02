using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Clients;

namespace NzbWebDAV.Api.Controllers.GetConnectionStats;

[ApiController]
[Route("api/usenet-providers/connection-stats")]
public class GetConnectionStatsController(UsenetProviderManager providerManager) : BaseApiController
{
    protected override Task<IActionResult> HandleRequest()
    {
        var providerStats = providerManager.Providers.Select(p => new
        {
            Id = p.ProviderId,
            Name = p.ProviderName,
            Host = p.Host,
            Port = p.Port,
            IsHealthy = p.IsHealthy,
            Priority = p.Priority,
            ActiveConnections = p.GetActiveConnectionCount(),
            MaxConnections = p.GetMaxConnectionCount()
        }).Cast<object>().ToArray();

        var totalActive = providerStats.Sum(p => ((dynamic)p).ActiveConnections);
        var totalMax = providerStats.Sum(p => ((dynamic)p).MaxConnections);

        var response = new GetConnectionStatsResponse
        {
            Status = true,
            ProviderStats = providerStats,
            TotalActiveConnections = totalActive,
            TotalMaxConnections = totalMax
        };

        return Task.FromResult<IActionResult>(Ok(response));
    }
}
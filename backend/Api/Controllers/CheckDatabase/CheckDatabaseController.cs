using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NzbWebDAV.Database;

namespace NzbWebDAV.Api.Controllers.CheckDatabase;

[ApiController]
[Route("api/check-database")]
public class CheckDatabaseController(DavDatabaseContext dbContext) : BaseApiController
{
    protected override async Task<IActionResult> HandleRequest()
    {
        try
        {
            // Get all config items
            var allConfigItems = await dbContext.ConfigItems.ToListAsync();
            
            // Separate legacy and provider configs
            var legacyConfig = allConfigItems.Where(c => c.ConfigName.StartsWith("usenet.") && !c.ConfigName.StartsWith("usenet.provider.")).ToList();
            var providerConfig = allConfigItems.Where(c => c.ConfigName.StartsWith("usenet.provider.")).ToList();
            var providerMeta = allConfigItems.Where(c => c.ConfigName.StartsWith("usenet.providers.")).ToList();
            
            var response = new CheckDatabaseResponse
            {
                Status = true,
                TotalConfigItems = allConfigItems.Count,
                LegacyUsenetConfig = legacyConfig.ToDictionary(c => c.ConfigName, c => c.ConfigValue),
                ProviderMetaConfig = providerMeta.ToDictionary(c => c.ConfigName, c => c.ConfigValue),
                ProviderSpecificConfig = providerConfig.ToDictionary(c => c.ConfigName, c => c.ConfigValue),
                AllConfigItems = allConfigItems.ToDictionary(c => c.ConfigName, c => c.ConfigValue)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return Ok(new CheckDatabaseResponse
            {
                Status = false,
                ErrorMessage = ex.Message
            });
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NzbWebDAV.Config;
using NzbWebDAV.Services;

namespace NzbWebDAV.Api.Controllers.TriggerMigration;

[ApiController]
[Route("api/migrate-usenet")]
public class TriggerMigrationController(ConfigManager configManager, ILogger<TriggerMigrationController> logger) : BaseApiController
{
    protected override async Task<IActionResult> HandleRequest()
    {
        try
        {
            var migrationLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<UsenetMigrationService>();
            var migrationService = new UsenetMigrationService(configManager, migrationLogger);
            var result = await migrationService.MigrateLegacyConfigToMultiProvider();
            
            var response = new TriggerMigrationResponse
            {
                Status = true,
                MigrationSuccessful = result,
                Message = result ? "Migration completed successfully" : "Migration failed or no legacy config found",
                ProviderCount = configManager.GetProviderCount()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Migration failed: {Message}", ex.Message);
            return Ok(new TriggerMigrationResponse
            {
                Status = true,
                MigrationSuccessful = false,
                Message = $"Migration failed: {ex.Message}",
                ProviderCount = 0
            });
        }
    }
}
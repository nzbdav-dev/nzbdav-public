using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Config;

namespace NzbWebDAV.Api.Controllers.DebugConfig;

[ApiController]
[Route("api/debug/config")]
public class DebugConfigController(ConfigManager configManager) : BaseApiController
{
    protected override Task<IActionResult> HandleRequest()
    {
        var allConfig = new Dictionary<string, string?>();
        
        // Legacy keys
        var legacyKeys = new[] { "usenet.host", "usenet.port", "usenet.use-ssl", "usenet.connections", "usenet.user", "usenet.pass" };
        foreach (var key in legacyKeys)
        {
            allConfig[key] = configManager.GetConfigValue(key);
        }
        
        // Provider keys
        allConfig["usenet.providers.count"] = configManager.GetConfigValue("usenet.providers.count");
        allConfig["usenet.providers.primary"] = configManager.GetConfigValue("usenet.providers.primary");
        
        // Check for provider 0
        var providerKeys = new[] { "name", "host", "port", "use-ssl", "connections", "user", "pass", "priority", "enabled" };
        foreach (var key in providerKeys)
        {
            allConfig[$"usenet.provider.0.{key}"] = configManager.GetConfigValue($"usenet.provider.0.{key}");
        }
        
        var response = new DebugConfigResponse
        {
            Status = true,
            Config = allConfig,
            ProviderCount = configManager.GetProviderCount(),
            AllProviders = configManager.GetAllProviderConfigurations()
        };

        return Task.FromResult<IActionResult>(Ok(response));
    }
}
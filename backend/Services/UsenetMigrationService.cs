using Microsoft.Extensions.Logging;
using NzbWebDAV.Config;
using NzbWebDAV.Database.Models;

namespace NzbWebDAV.Services;

public class UsenetMigrationService
{
    private readonly ConfigManager _configManager;
    private readonly ILogger<UsenetMigrationService> _logger;

    public UsenetMigrationService(ConfigManager configManager, ILogger<UsenetMigrationService> logger)
    {
        _configManager = configManager;
        _logger = logger;
    }

    /// <summary>
    /// Migrates legacy single provider configuration to multi-provider format
    /// </summary>
    public Task<bool> MigrateLegacyConfigToMultiProvider()
    {
        try
        {
            _logger.LogInformation("Starting migration from legacy single provider to multi-provider configuration");

            // Check if already using multi-provider
            var providerCount = _configManager.GetProviderCount();
            if (providerCount > 0)
            {
                _logger.LogInformation("Multi-provider configuration already exists with {Count} providers", providerCount);
                return Task.FromResult(true);
            }

            // Check if legacy configuration exists
            var legacyHost = _configManager.GetConfigValue("usenet.host");
            if (string.IsNullOrEmpty(legacyHost))
            {
                _logger.LogInformation("No legacy configuration found to migrate");
                return Task.FromResult(true);
            }

            // Gather legacy configuration
            var legacyConfig = new Dictionary<string, string>
            {
                ["name"] = "Migrated Provider",
                ["host"] = legacyHost,
                ["port"] = _configManager.GetConfigValue("usenet.port") ?? "563",
                ["use-ssl"] = _configManager.GetConfigValue("usenet.use-ssl") ?? "true",
                ["user"] = _configManager.GetConfigValue("usenet.user") ?? "",
                ["pass"] = _configManager.GetConfigValue("usenet.pass") ?? "",
                ["connections"] = _configManager.GetConfigValue("usenet.connections") ?? "10",
                ["priority"] = "0",
                ["enabled"] = "true"
            };

            // Validate required fields
            if (string.IsNullOrEmpty(legacyConfig["user"]) || string.IsNullOrEmpty(legacyConfig["pass"]))
            {
                _logger.LogWarning("Legacy configuration is incomplete - missing user or password");
                return Task.FromResult(false);
            }

            // Create multi-provider configuration
            var configItems = new List<ConfigItem>
            {
                new() { ConfigName = "usenet.providers.count", ConfigValue = "1" },
                new() { ConfigName = "usenet.providers.primary", ConfigValue = "0" }
            };

            // Add provider-specific configuration
            foreach (var kvp in legacyConfig)
            {
                configItems.Add(new ConfigItem 
                { 
                    ConfigName = $"usenet.provider.0.{kvp.Key}", 
                    ConfigValue = kvp.Value 
                });
            }

            // Update configuration
            _configManager.UpdateValues(configItems);

            _logger.LogInformation("Successfully migrated legacy configuration to multi-provider format");
            _logger.LogInformation("Migrated provider: {Name} at {Host}:{Port} (SSL: {SSL}, Connections: {Connections})", 
                legacyConfig["name"], legacyConfig["host"], legacyConfig["port"], legacyConfig["use-ssl"], legacyConfig["connections"]);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate legacy configuration: {Message}", ex.Message);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Validates the current multi-provider configuration
    /// </summary>
    public bool ValidateMultiProviderConfiguration()
    {
        try
        {
            var providerCount = _configManager.GetProviderCount();
            if (providerCount == 0)
            {
                _logger.LogWarning("No providers configured");
                return false;
            }

            var validProviders = 0;
            var enabledProviders = 0;

            for (int i = 0; i < providerCount; i++)
            {
                var isValid = ValidateProviderConfiguration(i);
                if (isValid)
                {
                    validProviders++;
                    if (_configManager.IsProviderEnabled(i))
                    {
                        enabledProviders++;
                    }
                }
            }

            _logger.LogInformation("Configuration validation: {ValidProviders}/{TotalProviders} valid, {EnabledProviders} enabled", 
                validProviders, providerCount, enabledProviders);

            return enabledProviders > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate multi-provider configuration: {Message}", ex.Message);
            return false;
        }
    }

    private bool ValidateProviderConfiguration(int index)
    {
        var config = _configManager.GetProviderConfiguration(index);
        
        var requiredFields = new[] { "host", "port", "user", "pass", "connections" };
        var missingFields = requiredFields.Where(field => !config.ContainsKey(field) || string.IsNullOrEmpty(config[field])).ToList();
        
        if (missingFields.Any())
        {
            _logger.LogWarning("Provider {Index} is missing required fields: {MissingFields}", 
                index, string.Join(", ", missingFields));
            return false;
        }

        // Validate numeric fields
        if (!int.TryParse(config["port"], out var port) || port <= 0 || port > 65535)
        {
            _logger.LogWarning("Provider {Index} has invalid port: {Port}", index, config["port"]);
            return false;
        }

        if (!int.TryParse(config["connections"], out var connections) || connections <= 0)
        {
            _logger.LogWarning("Provider {Index} has invalid connections: {Connections}", index, config["connections"]);
            return false;
        }

        return true;
    }
}
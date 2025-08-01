using System.Collections.Immutable;

namespace NzbWebDAV.Database.Models;

public class ConfigItem
{
    public static readonly ImmutableHashSet<string> Keys = ImmutableHashSet.Create([
        "api.key",
        "api.categories",
        // Legacy single provider keys (maintained for backward compatibility)
        "usenet.host",
        "usenet.port",
        "usenet.use-ssl",
        "usenet.connections",
        "usenet.user",
        "usenet.pass",
        // Multi-provider configuration
        "usenet.providers.count",
        "usenet.providers.primary",
        // Provider-specific keys (dynamically validated)
        // Format: usenet.provider.{index}.{property}
        // Properties: name, host, port, use-ssl, connections, user, pass, priority, enabled
        "webdav.user",
        "webdav.pass",
        "rclone.mount-dir",
    ]);

    public string ConfigName { get; set; } = null!;
    public string ConfigValue { get; set; } = null!;
    
    /// <summary>
    /// Validates if a configuration key is valid (including dynamic provider keys)
    /// </summary>
    public static bool IsValidConfigKey(string configName)
    {
        if (Keys.Contains(configName))
            return true;
            
        // Check for dynamic provider keys: usenet.provider.{index}.{property}
        if (configName.StartsWith("usenet.provider."))
        {
            var parts = configName.Split('.');
            if (parts.Length == 4 && 
                parts[0] == "usenet" && 
                parts[1] == "provider" &&
                int.TryParse(parts[2], out _))
            {
                var property = parts[3];
                return property is "name" or "host" or "port" or "use-ssl" or 
                               "connections" or "user" or "pass" or "priority" or "enabled";
            }
        }
        
        return false;
    }
}
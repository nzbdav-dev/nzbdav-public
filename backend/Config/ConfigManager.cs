using Microsoft.EntityFrameworkCore;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Utils;

namespace NzbWebDAV.Config;

public class ConfigManager
{
    private readonly Dictionary<string, string> _config = new();
    public event EventHandler<ConfigEventArgs>? OnConfigChanged;

    public async Task LoadConfig()
    {
        await using var dbContext = new DavDatabaseContext();
        var configItems = await dbContext.ConfigItems.ToListAsync();
        lock (_config)
        {
            _config.Clear();
            foreach (var configItem in configItems)
            {
                _config[configItem.ConfigName] = configItem.ConfigValue;
            }
        }
    }

    public string? GetConfigValue(string configName)
    {
        lock (_config)
        {
            return _config.TryGetValue(configName, out string? value) ? value : null;
        }
    }

    public void UpdateValues(List<ConfigItem> configItems)
    {
        lock (_config)
        {
            foreach (var configItem in configItems)
            {
                _config[configItem.ConfigName] = configItem.ConfigValue;
            }

            OnConfigChanged?.Invoke(this, new ConfigEventArgs
            {
                ChangedConfig = configItems.ToDictionary(x => x.ConfigName, x => x.ConfigValue),
                NewConfig = _config
            });
        }
    }

    public string GetRcloneMountDir()
    {
        return StringUtil.EmptyToNull(GetConfigValue("rclone.mount-dir"))
               ?? StringUtil.EmptyToNull(Environment.GetEnvironmentVariable("MOUNT_DIR"))
               ?? "/tmp";
    }

    public string GetApiKey()
    {
        return StringUtil.EmptyToNull(GetConfigValue("api.key"))
               ?? EnvironmentUtil.GetVariable("FRONTEND_BACKEND_API_KEY");
    }

    public string GetApiCategories()
    {
        return StringUtil.EmptyToNull(GetConfigValue("api.categories"))
               ?? StringUtil.EmptyToNull(Environment.GetEnvironmentVariable("CATEGORIES"))
               ?? "audio,software,tv,movies";
    }

    public int GetConnectionsPerStream()
    {
        return int.Parse(
            StringUtil.EmptyToNull(GetConfigValue("usenet.connections-per-stream"))
            ?? StringUtil.EmptyToNull(Environment.GetEnvironmentVariable("CONNECTIONS_PER_STREAM"))
            ?? "1"
        );
    }

    public int GetProviderCount()
    {
        var countStr = GetConfigValue("usenet.providers.count");
        return int.TryParse(countStr, out var count) ? count : 0;
    }

    public int GetPrimaryProviderIndex()
    {
        var primaryStr = GetConfigValue("usenet.providers.primary");
        return int.TryParse(primaryStr, out var index) ? index : 0;
    }

    public string? GetProviderConfigValue(int providerIndex, string property)
    {
        return GetConfigValue($"usenet.provider.{providerIndex}.{property}");
    }

    public bool IsProviderEnabled(int providerIndex)
    {
        var enabled = GetProviderConfigValue(providerIndex, "enabled");
        return bool.TryParse(enabled, out var result) && result;
    }

    public Dictionary<string, string> GetProviderConfiguration(int providerIndex)
    {
        var config = new Dictionary<string, string>();
        var properties = new[] { "name", "host", "port", "use-ssl", "connections", "user", "pass", "priority", "enabled" };
        
        foreach (var property in properties)
        {
            var value = GetProviderConfigValue(providerIndex, property);
            if (!string.IsNullOrEmpty(value))
            {
                config[property] = value;
            }
        }
        
        return config;
    }

    public List<Dictionary<string, string>> GetAllProviderConfigurations()
    {
        var providers = new List<Dictionary<string, string>>();
        var count = GetProviderCount();
        
        for (int i = 0; i < count; i++)
        {
            var providerConfig = GetProviderConfiguration(i);
            if (providerConfig.Count > 0)
            {
                providerConfig["index"] = i.ToString();
                providers.Add(providerConfig);
            }
        }
        
        return providers;
    }

    public string? GetWebdavUser()
    {
        return StringUtil.EmptyToNull(GetConfigValue("webdav.user"))
               ?? StringUtil.EmptyToNull(Environment.GetEnvironmentVariable("WEBDAV_USER"));
    }

    public string? GetWebdavPasswordHash()
    {
        var hashedPass = StringUtil.EmptyToNull(GetConfigValue("webdav.pass"));
        if (hashedPass != null) return hashedPass;
        var pass = Environment.GetEnvironmentVariable("WEBDAV_PASSWORD");
        if (pass != null) return PasswordUtil.Hash(pass);
        return null;
    }

    public class ConfigEventArgs : EventArgs
    {
        public Dictionary<string, string> ChangedConfig { get; set; } = new();
        public Dictionary<string, string> NewConfig { get; set; } = new();
    }
}
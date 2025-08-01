using Microsoft.Extensions.Logging;
using NzbWebDAV.Config;
using NzbWebDAV.Streams;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Clients;

public class UsenetProviderManager : IDisposable
{
    private readonly List<IUsenetProvider> _providers = new();
    private readonly ILogger<UsenetProviderManager> _logger;
    private readonly ConfigManager _configManager;
    private int _primaryProviderIndex = 0;

    public UsenetProviderManager(ConfigManager configManager, ILogger<UsenetProviderManager> logger)
    {
        _configManager = configManager;
        _logger = logger;
        
        LoadProvidersFromConfig();
        
        // Subscribe to configuration changes
        _configManager.OnConfigChanged += OnConfigChanged;
    }

    public IReadOnlyList<IUsenetProvider> Providers => _providers.AsReadOnly();

    private void LoadProvidersFromConfig()
    {
        // Clear existing providers
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }
        _providers.Clear();

        // Load provider count
        var countStr = _configManager.GetConfigValue("usenet.providers.count");
        if (!int.TryParse(countStr, out var providerCount) || providerCount <= 0)
        {
            _logger.LogWarning("No providers configured or invalid provider count: {Count}", countStr);
            return;
        }

        // Load primary provider index
        var primaryStr = _configManager.GetConfigValue("usenet.providers.primary");
        if (int.TryParse(primaryStr, out var primaryIndex) && primaryIndex >= 0 && primaryIndex < providerCount)
        {
            _primaryProviderIndex = primaryIndex;
        }

        // Load each provider
        _logger.LogInformation("Loading {Count} providers", providerCount);
        for (int i = 0; i < providerCount; i++)
        {
            try
            {
                var provider = LoadProvider(i);
                if (provider != null)
                {
                    _logger.LogInformation("Successfully loaded provider {Index}: {Name} ({Host}:{Port})", 
                        i, provider.ProviderName, provider.Host, provider.Port);
                    _providers.Add(provider);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load provider {Index}: {Message}", i, ex.Message);
            }
        }

        // Sort providers by priority
        _providers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        
        _logger.LogInformation("Loaded {Count} Usenet providers", _providers.Count);
    }

    private SingleUsenetProvider? LoadProvider(int index)
    {
        var enabled = _configManager.GetConfigValue($"usenet.provider.{index}.enabled");
        if (enabled != "true")
        {
            _logger.LogDebug("Provider {Index} is disabled", index);
            return null;
        }

        var name = _configManager.GetConfigValue($"usenet.provider.{index}.name") ?? $"Provider {index}";
        var host = _configManager.GetConfigValue($"usenet.provider.{index}.host");
        var portStr = _configManager.GetConfigValue($"usenet.provider.{index}.port");
        var useSslStr = _configManager.GetConfigValue($"usenet.provider.{index}.use-ssl");
        var user = _configManager.GetConfigValue($"usenet.provider.{index}.user");
        var pass = _configManager.GetConfigValue($"usenet.provider.{index}.pass");
        var connectionsStr = _configManager.GetConfigValue($"usenet.provider.{index}.connections");
        var priorityStr = _configManager.GetConfigValue($"usenet.provider.{index}.priority");

        if (string.IsNullOrEmpty(host) || !int.TryParse(portStr, out var port) ||
            string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass) ||
            !int.TryParse(connectionsStr, out var connections))
        {
            _logger.LogWarning("Invalid configuration for provider {Index}: missing host={Host}, port={Port}, user={User}, connections={Connections}", 
                index, string.IsNullOrEmpty(host), string.IsNullOrEmpty(portStr), string.IsNullOrEmpty(user), string.IsNullOrEmpty(connectionsStr));
            return null;
        }

        var useSsl = bool.Parse(useSslStr ?? "true");
        var priority = int.TryParse(priorityStr, out var p) ? p : index;

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var providerLogger = loggerFactory.CreateLogger<SingleUsenetProvider>();

        return new SingleUsenetProvider(
            index.ToString(),
            name,
            host,
            port,
            useSsl,
            user,
            pass,
            connections,
            priority,
            providerLogger);
    }

    private void OnConfigChanged(object? sender, ConfigManager.ConfigEventArgs args)
    {
        // Check if any provider-related configuration changed
        var providerConfigChanged = args.ChangedConfig.Keys.Any(key => 
            key.StartsWith("usenet.provider.") || 
            key.StartsWith("usenet.providers."));

        if (providerConfigChanged)
        {
            _logger.LogInformation("Provider configuration changed, reloading providers");
            LoadProvidersFromConfig();
        }
    }

    public async Task<bool> CheckNzbFileHealthAsync(NzbFile nzbFile, CancellationToken cancellationToken = default)
    {
        var healthyProviders = GetHealthyProviders();
        if (!healthyProviders.Any())
        {
            _logger.LogError("No healthy providers available for health check");
            return false;
        }

        foreach (var provider in healthyProviders)
        {
            try
            {
                var isHealthy = await provider.CheckNzbFileHealthAsync(nzbFile, cancellationToken);
                if (isHealthy)
                {
                    _logger.LogDebug("File health check successful with provider {ProviderName}", provider.ProviderName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check failed with provider {ProviderName}", provider.ProviderName);
            }
        }

        _logger.LogWarning("File health check failed with all providers");
        return false;
    }

    public async Task<NzbFileStream> GetFileStreamAsync(NzbFile nzbFile, int concurrentConnections, CancellationToken cancellationToken = default)
    {
        var healthyProviders = GetHealthyProviders();
        if (!healthyProviders.Any())
        {
            throw new InvalidOperationException("No healthy providers available");
        }

        Exception? lastException = null;
        var attemptedProviders = new List<string>();
        
        foreach (var provider in healthyProviders)
        {
            try
            {
                attemptedProviders.Add(provider.ProviderName);
                _logger.LogInformation("Attempting to get file stream from provider {ProviderName} (priority {Priority})", 
                    provider.ProviderName, provider.Priority);
                var stream = await provider.GetFileStreamAsync(nzbFile, concurrentConnections, cancellationToken);
                _logger.LogInformation("✓ Successfully got file stream from provider {ProviderName} after {AttemptCount} attempts", 
                    provider.ProviderName, attemptedProviders.Count);
                return stream;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get file stream from provider {ProviderName}: {ErrorMessage}", 
                    provider.ProviderName, ex.Message);
                lastException = ex;
            }
        }

        _logger.LogError("All {ProviderCount} providers failed to provide file stream. Attempted providers: {AttemptedProviders}", 
            attemptedProviders.Count, string.Join(", ", attemptedProviders));
        throw new InvalidOperationException($"All {attemptedProviders.Count} providers failed to provide file stream", lastException);
    }

    public IReadOnlyList<IUsenetProvider> GetAllProviders()
    {
        return _providers.AsReadOnly();
    }

    public Stream GetFileStream(string[] segmentIds, long fileSize, int concurrentConnections)
    {
        var healthyProviders = GetHealthyProviders();
        if (!healthyProviders.Any())
        {
            throw new InvalidOperationException("No healthy providers available");
        }

        Exception? lastException = null;
        foreach (var provider in healthyProviders)
        {
            try
            {
                var stream = provider.GetFileStream(segmentIds, fileSize, concurrentConnections);
                _logger.LogDebug("Successfully got file stream from provider {ProviderName}", provider.ProviderName);
                return stream;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get file stream from provider {ProviderName}", provider.ProviderName);
                lastException = ex;
            }
        }

        throw new InvalidOperationException("All providers failed to provide file stream", lastException);
    }

    public async Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken = default)
    {
        var healthyProviders = GetHealthyProviders();
        if (!healthyProviders.Any())
        {
            throw new InvalidOperationException("No healthy providers available");
        }

        Exception? lastException = null;
        foreach (var provider in healthyProviders)
        {
            try
            {
                var header = await provider.GetSegmentYencHeaderAsync(segmentId, cancellationToken);
                _logger.LogDebug("Successfully got YENC header from provider {ProviderName}", provider.ProviderName);
                return header;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get YENC header from provider {ProviderName}", provider.ProviderName);
                lastException = ex;
            }
        }

        throw new InvalidOperationException("All providers failed to provide YENC header", lastException);
    }

    public async Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken = default)
    {
        var healthyProviders = GetHealthyProviders();
        if (!healthyProviders.Any())
        {
            throw new InvalidOperationException("No healthy providers available");
        }

        Exception? lastException = null;
        foreach (var provider in healthyProviders)
        {
            try
            {
                var size = await provider.GetFileSizeAsync(file, cancellationToken);
                _logger.LogDebug("Successfully got file size from provider {ProviderName}", provider.ProviderName);
                return size;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get file size from provider {ProviderName}", provider.ProviderName);
                lastException = ex;
            }
        }

        throw new InvalidOperationException("All providers failed to provide file size", lastException);
    }

    public async Task<bool> TestConnectionAsync(string providerId, CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderId == providerId);
        if (provider == null)
        {
            _logger.LogWarning("Provider {ProviderId} not found", providerId);
            return false;
        }

        return await provider.TestConnectionAsync(cancellationToken);
    }

    private IEnumerable<IUsenetProvider> GetHealthyProviders()
    {
        // First, try the primary provider if it's healthy
        if (_primaryProviderIndex < _providers.Count && _providers[_primaryProviderIndex].IsHealthy)
        {
            yield return _providers[_primaryProviderIndex];
        }

        // Then try other providers in priority order, excluding the primary
        foreach (var provider in _providers.Where((p, i) => i != _primaryProviderIndex && p.IsHealthy))
        {
            yield return provider;
        }
    }

    public void Dispose()
    {
        _configManager.OnConfigChanged -= OnConfigChanged;
        
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }
        _providers.Clear();
    }
}
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NzbWebDAV.Clients.Connections;
using NzbWebDAV.Exceptions;
using NzbWebDAV.Extensions;
using NzbWebDAV.Streams;
using Usenet.Nntp.Responses;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Clients;

public class SingleUsenetProvider : IUsenetProvider
{
    private readonly INntpClient _client;
    private readonly ILogger<SingleUsenetProvider> _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly bool _useSsl;
    private readonly string _user;
    private readonly string _pass;
    private readonly int _connections;
    private bool _isHealthy = true;
    private DateTime? _lastFailureTime;
    private readonly TimeSpan _recoveryInterval = TimeSpan.FromMinutes(5); // Try recovery every 5 minutes

    public SingleUsenetProvider(
        string providerId,
        string providerName,
        string host,
        int port,
        bool useSsl,
        string user,
        string pass,
        int connections,
        int priority,
        ILogger<SingleUsenetProvider> logger)
    {
        ProviderId = providerId;
        ProviderName = providerName;
        Priority = priority;
        _host = host;
        _port = port;
        _useSsl = useSsl;
        _user = user;
        _pass = pass;
        _connections = connections;
        _logger = logger;

        // Initialize the NNTP client
        var createNewConnection = (CancellationToken ct) => CreateNewConnection(_host, _port, _useSsl, _user, _pass, ct);
        ConnectionPool<INntpClient> connectionPool = new(_connections, createNewConnection);
        var multiConnectionClient = new MultiConnectionNntpClient(connectionPool);
        var cache = new MemoryCache(new MemoryCacheOptions() { SizeLimit = 8192 });
        _client = new CachingNntpClient(multiConnectionClient, cache);
    }

    public string ProviderId { get; }
    public string ProviderName { get; }
    public string Host => _host;
    public int Port => _port;
    public bool IsHealthy => _isHealthy || ShouldAttemptRecovery();
    public int Priority { get; }

    /// <summary>
    /// Manually resets the provider to healthy state, clearing any failure tracking.
    /// </summary>
    public void ResetHealth()
    {
        _isHealthy = true;
        _lastFailureTime = null;
        _logger.LogInformation("Provider '{ProviderName}' health manually reset", ProviderName);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await CreateNewConnection(_host, _port, _useSsl, _user, _pass, cancellationToken);
            if (!_isHealthy || _lastFailureTime != null)
                MarkAsRecovered("TestConnection");
            else
                _isHealthy = true;
            _logger.LogDebug("Connection test successful for provider '{ProviderName}'", ProviderName);
            return true;
        }
        catch (Exception ex)
        {
            HandleProviderException(ex, "Connection test");
            return false;
        }
    }

    public async Task<bool> CheckNzbFileHealthAsync(NzbFile nzbFile, CancellationToken cancellationToken = default)
    {
        try
        {
            var childCt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var tasks = nzbFile.Segments
                .Select(x => x.MessageId.Value)
                .Select(x => _client.StatAsync(x, childCt.Token))
                .ToHashSet();

            while (tasks.Count > 0)
            {
                var completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);

                var completedResult = await completedTask;
                if (completedResult.ResponseType != NntpStatResponseType.ArticleExists)
                {
                    await childCt.CancelAsync();
                    // Missing articles don't affect provider health - this is content availability, not provider connectivity
                    _logger.LogDebug("Health check found missing articles for provider '{ProviderName}' (content unavailable)", ProviderName);
                    return false;
                }
            }

            if (!_isHealthy || _lastFailureTime != null)
                MarkAsRecovered("CheckNzbFileHealth");
            else
                _isHealthy = true;
            return true;
        }
        catch (Exception ex)
        {
            HandleProviderException(ex, "Health check failed");
            return false;
        }
    }

    public async Task<NzbFileStream> GetFileStreamAsync(NzbFile nzbFile, int concurrentConnections, CancellationToken cancellationToken = default)
    {
        try
        {
            var firstSegmentId = nzbFile.GetOrderedSegmentIds().First();
            var firstSegmentStream = await _client.GetSegmentStreamAsync(firstSegmentId, cancellationToken);
            if (!_isHealthy || _lastFailureTime != null)
                MarkAsRecovered("GetFileStream");
            else
                _isHealthy = true;
            return new NzbFileStream(nzbFile, firstSegmentStream, _client, concurrentConnections);
        }
        catch (Exception ex)
        {
            HandleProviderException(ex, "Failed to get file stream");
            throw;
        }
    }

    public Stream GetFileStream(string[] segmentIds, long fileSize, int concurrentConnections)
    {
        try
        {
            if (!_isHealthy || _lastFailureTime != null)
                MarkAsRecovered("GetFileStream");
            else
                _isHealthy = true;
            return new NzbFileStream(segmentIds, fileSize, _client, concurrentConnections);
        }
        catch (Exception ex)
        {
            HandleProviderException(ex, "Failed to get file stream");
            throw;
        }
    }

    public async Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetSegmentYencHeaderAsync(segmentId, cancellationToken);
            if (!_isHealthy || _lastFailureTime != null)
                MarkAsRecovered("GetSegmentYencHeader");
            else
                _isHealthy = true;
            return result;
        }
        catch (Exception ex)
        {
            HandleProviderException(ex, "Failed to get YENC header");
            throw;
        }
    }

    public async Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetFileSizeAsync(file, cancellationToken);
            if (!_isHealthy || _lastFailureTime != null)
                MarkAsRecovered("GetFileSize");
            else
                _isHealthy = true;
            return result;
        }
        catch (Exception ex)
        {
            HandleProviderException(ex, "Failed to get file size");
            throw;
        }
    }

    public void UpdateConnectionPool(string host, int port, bool useSsl, string user, string pass, int connections)
    {
        _logger.LogDebug("Updating connection pool for provider '{ProviderName}'", ProviderName);
        
        if (_client is CachingNntpClient cachingClient &&
            cachingClient.GetInnerClient() is MultiConnectionNntpClient multiClient)
        {
            var createNewConnection = (CancellationToken ct) => CreateNewConnection(host, port, useSsl, user, pass, ct);
            multiClient.UpdateConnectionPool(new(connections, createNewConnection));
        }
    }

    /// <summary>
    /// Determines if the provider should attempt recovery based on the last failure time.
    /// </summary>
    private bool ShouldAttemptRecovery()
    {
        if (_isHealthy) return false;
        if (_lastFailureTime == null) return false;
        
        return DateTime.UtcNow - _lastFailureTime.Value >= _recoveryInterval;
    }
    
    /// <summary>
    /// Marks provider as recovered and resets failure tracking.
    /// </summary>
    private void MarkAsRecovered(string operation)
    {
        _isHealthy = true;
        _lastFailureTime = null;
        _logger.LogInformation("Provider '{ProviderName}' recovered after {Operation} succeeded", ProviderName, operation);
    }
    
    /// <summary>
    /// Handles exceptions and determines whether they should affect provider health.
    /// Content-related issues (missing articles) don't mark provider as unhealthy.
    /// Connectivity/authentication issues do mark provider as unhealthy.
    /// </summary>
    private void HandleProviderException(Exception ex, string operation)
    {
        if (IsContentException(ex))
        {
            // Content issues (missing articles, etc.) - don't affect provider health
            _logger.LogDebug(ex, "{Operation} failed for provider '{ProviderName}' - content issue: {ErrorMessage}", 
                operation, ProviderName, ex.Message);
        }
        else
        {
            // Provider connectivity/authentication issues - mark provider as unhealthy
            var wasHealthy = _isHealthy;
            _isHealthy = false;
            _lastFailureTime = DateTime.UtcNow;
            
            var logLevel = wasHealthy ? LogLevel.Warning : LogLevel.Debug;
            _logger.Log(logLevel, ex, "{Operation} failed for provider '{ProviderName}' - provider issue: {ErrorMessage}. Will retry in {RetryMinutes} minutes.", 
                operation, ProviderName, ex.Message, _recoveryInterval.TotalMinutes);
        }
    }

    /// <summary>
    /// Determines if an exception is related to content availability rather than provider connectivity.
    /// </summary>
    private static bool IsContentException(Exception ex)
    {
        return ex is UsenetArticleNotFoundException ||
               ex.Message.Contains("Article with message-id") ||
               ex.Message.Contains("not found") ||
               ex.Message.Contains("Missing part header") ||
               ex.Message.Contains("Received invalid response");
    }

    public static async ValueTask<INntpClient> CreateNewConnection(
        string host,
        int port,
        bool useSsl,
        string user,
        string pass,
        CancellationToken cancellationToken)
    {
        var connection = new ThreadSafeNntpClient();
        if (!await connection.ConnectAsync(host, port, useSsl, cancellationToken))
            throw new CouldNotConnectToUsenetException("Could not connect to usenet host. Check connection settings.");
        if (!await connection.AuthenticateAsync(user, pass, cancellationToken))
            throw new CouldNotLoginToUsenetException("Could not login to usenet host. Check username and password.");
        return connection;
    }

    public int GetActiveConnectionCount()
    {
        if (_client is CachingNntpClient cachingClient &&
            cachingClient.GetInnerClient() is MultiConnectionNntpClient multiClient)
        {
            return multiClient.GetActiveConnectionCount();
        }
        return 0;
    }

    public int GetMaxConnectionCount()
    {
        return _connections;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
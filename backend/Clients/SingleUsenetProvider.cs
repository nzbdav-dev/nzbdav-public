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
    public bool IsHealthy => _isHealthy;
    public int Priority { get; }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await CreateNewConnection(_host, _port, _useSsl, _user, _pass, cancellationToken);
            _isHealthy = true;
            _logger.LogInformation("Connection test successful for provider {ProviderName}", ProviderName);
            return true;
        }
        catch (Exception ex)
        {
            _isHealthy = false;
            _logger.LogWarning(ex, "Connection test failed for provider {ProviderName}", ProviderName);
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
                    _isHealthy = false;
                    return false;
                }
            }

            _isHealthy = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for provider {ProviderName}", ProviderName);
            _isHealthy = false;
            return false;
        }
    }

    public async Task<NzbFileStream> GetFileStreamAsync(NzbFile nzbFile, int concurrentConnections, CancellationToken cancellationToken = default)
    {
        try
        {
            var firstSegmentId = nzbFile.GetOrderedSegmentIds().First();
            var firstSegmentStream = await _client.GetSegmentStreamAsync(firstSegmentId, cancellationToken);
            _isHealthy = true;
            return new NzbFileStream(nzbFile, firstSegmentStream, _client, concurrentConnections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file stream for provider {ProviderName}", ProviderName);
            _isHealthy = false;
            throw;
        }
    }

    public Stream GetFileStream(string[] segmentIds, long fileSize, int concurrentConnections)
    {
        try
        {
            _isHealthy = true;
            return new NzbFileStream(segmentIds, fileSize, _client, concurrentConnections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file stream for provider {ProviderName}", ProviderName);
            _isHealthy = false;
            throw;
        }
    }

    public async Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetSegmentYencHeaderAsync(segmentId, cancellationToken);
            _isHealthy = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get YENC header for provider {ProviderName}", ProviderName);
            _isHealthy = false;
            throw;
        }
    }

    public async Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetFileSizeAsync(file, cancellationToken);
            _isHealthy = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file size for provider {ProviderName}", ProviderName);
            _isHealthy = false;
            throw;
        }
    }

    public void UpdateConnectionPool(string host, int port, bool useSsl, string user, string pass, int connections)
    {
        _logger.LogInformation("Updating connection pool for provider {ProviderName}", ProviderName);
        
        if (_client is CachingNntpClient cachingClient &&
            cachingClient.GetInnerClient() is MultiConnectionNntpClient multiClient)
        {
            var createNewConnection = (CancellationToken ct) => CreateNewConnection(host, port, useSsl, user, pass, ct);
            multiClient.UpdateConnectionPool(new(connections, createNewConnection));
        }
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

    public void Dispose()
    {
        _client?.Dispose();
    }
}
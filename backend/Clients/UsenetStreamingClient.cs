using Microsoft.Extensions.Caching.Memory;
using NzbWebDAV.Clients.Connections;
using NzbWebDAV.Config;
using NzbWebDAV.Exceptions;
using NzbWebDAV.Extensions;
using NzbWebDAV.Streams;
using Usenet.Nntp.Responses;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Clients;

public class UsenetStreamingClient
{
    private readonly INntpClient _client;

    public UsenetStreamingClient(ConfigManager configManager)
    {
        // get connection settings from config-manager
        var host = configManager.GetConfigValue("usenet.host") ?? string.Empty;
        var port = int.Parse(configManager.GetConfigValue("usenet.port") ?? "119");
        var useSsl = bool.Parse(configManager.GetConfigValue("usenet.use-ssl") ?? "false");
        var user = configManager.GetConfigValue("usenet.user") ?? string.Empty;
        var pass = configManager.GetConfigValue("usenet.pass") ?? string.Empty;
        var connections = int.Parse(configManager.GetConfigValue("usenet.connections") ?? "10");

        // initialize the nntp-client
        var createNewConnection = (CancellationToken ct) => CreateNewConnection(host, port, useSsl, user, pass, ct);
        ConnectionPool<INntpClient> connectionPool = new(connections, createNewConnection);
        var multiConnectionClient = new MultiConnectionNntpClient(connectionPool);
        
        // Adaptive cache sizing based on available memory and connection count
        var baseCacheSize = Math.Max(16384, connections * 512); // Minimum 16K, scale with connections
        var maxCacheSize = Math.Min(baseCacheSize, 65536); // Cap at 64K entries
        var cache = new MemoryCache(new MemoryCacheOptions() 
        { 
            SizeLimit = maxCacheSize,
            CompactionPercentage = 0.25 // Compact when 75% full
        });
        _client = new CachingNntpClient(multiConnectionClient, cache);

        // when config changes, update the connection-pool
        configManager.OnConfigChanged += (_, configEventArgs) =>
        {
            // if unrelated config changed, do nothing
            if (!configEventArgs.ChangedConfig.ContainsKey("usenet.host") &&
                !configEventArgs.ChangedConfig.ContainsKey("usenet.port") &&
                !configEventArgs.ChangedConfig.ContainsKey("usenet.use-ssl") &&
                !configEventArgs.ChangedConfig.ContainsKey("usenet.user") &&
                !configEventArgs.ChangedConfig.ContainsKey("usenet.pass") &&
                !configEventArgs.ChangedConfig.ContainsKey("usenet.connections")) return;

            // update the connection-pool according to the new config
            var connectionCount = int.Parse(configEventArgs.NewConfig["usenet.connections"]);
            var newHost = configEventArgs.NewConfig["usenet.host"];
            var newPort = int.Parse(configEventArgs.NewConfig["usenet.port"]);
            var newUseSsl = bool.Parse(configEventArgs.NewConfig.GetValueOrDefault("usenet.use-ssl", "false"));
            var newUser = configEventArgs.NewConfig["usenet.user"];
            var newPass = configEventArgs.NewConfig["usenet.pass"];
            multiConnectionClient.UpdateConnectionPool(new(connectionCount, cancellationToken =>
                CreateNewConnection(newHost, newPort, newUseSsl, newUser, newPass, cancellationToken)));
        };
    }

    public async Task<bool> CheckNzbFileHealth(NzbFile nzbFile, CancellationToken cancellationToken = default)
    {
        using var childCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var childCt = childCts.Token;
        
        // Batch process segments in parallel groups to avoid overwhelming the connection pool
        const int batchSize = 20; // Process 20 segments at a time
        var segmentIds = nzbFile.Segments.Select(x => x.MessageId.Value).ToArray();
        
        for (int i = 0; i < segmentIds.Length; i += batchSize)
        {
            var batch = segmentIds.Skip(i).Take(batchSize);
            var batchTasks = batch.Select(segmentId => _client.StatAsync(segmentId, childCt)).ToArray();
            
            try
            {
                var results = await Task.WhenAll(batchTasks);
                
                // Check if any segment in this batch is missing
                if (results.Any(r => r.ResponseType != NntpStatResponseType.ArticleExists))
                {
                    return false;
                }
            }
            catch (OperationCanceledException) when (childCt.IsCancellationRequested)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<NzbFileStream> GetFileStream(NzbFile nzbFile, int concurrentConnections, CancellationToken ct)
    {
        var firstSegmentId = nzbFile.GetOrderedSegmentIds().First();
        var firstSegmentStream = await _client.GetSegmentStreamAsync(firstSegmentId, ct);
        return new NzbFileStream(nzbFile, firstSegmentStream, _client, concurrentConnections);
    }

    public Stream GetFileStream(string[] segmentIds, long fileSize, int concurrentConnections)
    {
        return new NzbFileStream(segmentIds, fileSize, _client, concurrentConnections);
    }

    public Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken)
    {
        return _client.GetSegmentYencHeaderAsync(segmentId, cancellationToken);
    }

    public Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken)
    {
        return _client.GetFileSizeAsync(file, cancellationToken);
    }

    public static async ValueTask<INntpClient> CreateNewConnection
    (
        string host,
        int port,
        bool useSsl,
        string user,
        string pass,
        CancellationToken cancellationToken
    )
    {
        var connection = new ThreadSafeNntpClient();
        if (!await connection.ConnectAsync(host, port, useSsl, cancellationToken))
            throw new CouldNotConnectToUsenetException("Could not connect to usenet host. Check connection settings.");
        if (!await connection.AuthenticateAsync(user, pass, cancellationToken))
            throw new CouldNotLoginToUsenetException("Could not login to usenet host. Check username and password.");
        return connection;
    }
}
using NWebDav.Server.Stores;
using NzbWebDAV.Clients;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Services;
using Microsoft.Extensions.Logging;

namespace NzbWebDAV.WebDav;

public class DatabaseStore(
    DavDatabaseClient dbClient,
    ConfigManager configManager,
    UsenetProviderManager usenetClient,
    QueueManager queueManager,
    ILoggerFactory loggerFactory
) : IStore
{
    private readonly DatabaseStoreCollection _root = new(
        DavItem.Root,
        dbClient,
        configManager,
        usenetClient,
        queueManager,
        loggerFactory
    );

    public async Task<IStoreItem?> GetItemAsync(string path, CancellationToken cancellationToken)
    {
        path = path.Trim('/');
        return path == "" ? _root : await _root.ResolvePath(path, cancellationToken);
    }

    public Task<IStoreItem?> GetItemAsync(Uri uri, CancellationToken cancellationToken)
    {
        return GetItemAsync(Uri.UnescapeDataString(uri.AbsolutePath), cancellationToken);
    }

    public async Task<IStoreCollection?> GetCollectionAsync(Uri uri, CancellationToken cancellationToken)
    {
        return await GetItemAsync(uri, cancellationToken) as IStoreCollection;
    }
}
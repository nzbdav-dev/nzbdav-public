using NWebDav.Server.Stores;
using NzbWebDAV.Clients;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Services;
using Microsoft.Extensions.Caching.Memory;

namespace NzbWebDAV.WebDav;

public class DatabaseStore(
    DavDatabaseClient dbClient,
    ConfigManager configManager,
    UsenetStreamingClient usenetClient,
    QueueManager queueManager,
    IMemoryCache memoryCache
) : IStore
{
    private readonly DatabaseStoreCollection _root = new(
        DavItem.Root,
        dbClient,
        configManager,
        usenetClient,
        queueManager
    );

    public async Task<IStoreItem?> GetItemAsync(string path, CancellationToken cancellationToken)
    {
        path = path.Trim('/');
        if (path == "") 
            return _root;

        // Try direct path resolution first for better performance
        var directItem = await dbClient.ResolvePathAsync(path, cancellationToken);
        if (directItem != null)
        {
            return GetItem(directItem);
        }

        // Fallback to recursive path resolution
        return await _root.ResolvePath(path, cancellationToken);
    }

    public Task<IStoreItem?> GetItemAsync(Uri uri, CancellationToken cancellationToken)
    {
        return GetItemAsync(Uri.UnescapeDataString(uri.AbsolutePath), cancellationToken);
    }

    public async Task<IStoreCollection?> GetCollectionAsync(Uri uri, CancellationToken cancellationToken)
    {
        return await GetItemAsync(uri, cancellationToken) as IStoreCollection;
    }

    private IStoreItem GetItem(DavItem davItem)
    {
        return davItem.Type switch
        {
            DavItem.ItemType.Directory when davItem.Id == DavItem.NzbFolder.Id =>
                new DatabaseStoreWatchFolder(davItem, dbClient, configManager, usenetClient, queueManager),
            DavItem.ItemType.Directory when davItem.Id == DavItem.ContentFolder.Id =>
                new DatabaseStoreCollection(davItem, dbClient, configManager, usenetClient, queueManager),
            DavItem.ItemType.Directory when davItem.Id == DavItem.SymlinkFolder.Id =>
                new DatabaseStoreSymlinkCollection(davItem, dbClient, configManager, memoryCache),
            DavItem.ItemType.Directory =>
                new DatabaseStoreCollection(davItem, dbClient, configManager, usenetClient, queueManager),
            DavItem.ItemType.SymlinkRoot =>
                new DatabaseStoreSymlinkCollection(davItem, dbClient, configManager, memoryCache),
            DavItem.ItemType.NzbFile =>
                new DatabaseStoreNzbFile(davItem, dbClient, usenetClient, configManager),
            DavItem.ItemType.RarFile =>
                new DatabaseStoreRarFile(davItem, dbClient, usenetClient, configManager),
            _ => throw new ArgumentException("Unrecognized directory child type.")
        };
    }
}
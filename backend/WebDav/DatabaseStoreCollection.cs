using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.Clients;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Extensions;
using NzbWebDAV.Queue;
using NzbWebDAV.WebDav.Base;
using NzbWebDAV.WebDav.Requests;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreCollection(
    DavItem davDirectory,
    DavDatabaseClient dbClient,
    ConfigManager configManager,
    UsenetStreamingClient usenetClient,
    QueueManager queueManager
) : BaseStoreCollection
{
    public override string Name => davDirectory.Name;
    public override string UniqueKey => davDirectory.Id.ToString();

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        throw new InvalidOperationException("Files and Directories cannot be copied.");
    }

    protected override async Task<IStoreItem?> GetItemAsync(GetItemRequest request)
    {
        var child = await dbClient.GetDirectoryChildAsync(davDirectory.Id, request.Name, request.CancellationToken);
        if (child is null) return null;
        return GetItem(child);
    }

    protected override async Task<IStoreItem[]> GetAllItemsAsync(CancellationToken cancellationToken)
    {
        return (await dbClient.GetDirectoryChildrenAsync(davDirectory.Id, cancellationToken))
            .Select(GetItem)
            .ToArray();
    }

    protected override Task<StoreItemResult> CreateItemAsync(CreateItemRequest request)
    {
        throw new InvalidOperationException("NZBs can only be added to the `/nzbs` folder.");
    }

    protected override Task<StoreCollectionResult> CreateCollectionAsync(CreateCollectionRequest request)
    {
        throw new InvalidOperationException("Directories cannot be created.");
    }

    protected override bool SupportsFastMove(SupportsFastMoveRequest request)
    {
        return false;
    }

    protected override Task<StoreItemResult> MoveItemAsync(MoveItemRequest request)
    {
        throw new InvalidOperationException("Files and Directories cannot be moved.");
    }

    protected override async Task<DavStatusCode> DeleteItemAsync(DeleteItemRequest request)
    {
        // Cannot delete items from dav root.
        if (davDirectory.Id == DavItem.Root.Id)
            return DavStatusCode.Forbidden;

        // Get the item being deleted
        var davItem = await dbClient.GetDirectoryChildAsync(davDirectory.Id, request.Name, request.CancellationToken);
        if (davItem is null) return DavStatusCode.NotFound;

        // If the item is a file, simply delete it and we're done.
        if (davItem.Type is DavItem.ItemType.NzbFile or DavItem.ItemType.RarFile)
        {
            dbClient.Ctx.Items.Remove(davItem);
            await dbClient.Ctx.SaveChangesAsync();
            return DavStatusCode.Ok;
        }

        // If the item is a directory and it not a protected directory, simply delete it.
        if (davItem.Type == DavItem.ItemType.Directory && !davItem.IsProtected())
        {
            dbClient.Ctx.Items.Remove(davItem);
            await dbClient.Ctx.SaveChangesAsync();
            return DavStatusCode.Ok;
        }

        // forbid deletion of any other items
        return DavStatusCode.Forbidden;
    }

    private IStoreItem GetItem(DavItem davItem)
    {
        return davItem.Type switch
        {
            DavItem.ItemType.Directory when davItem.Id == DavItem.NzbFolder.Id =>
                new DatabaseStoreWatchFolder(davItem, dbClient, configManager, usenetClient, queueManager),
            DavItem.ItemType.Directory =>
                new DatabaseStoreCollection(davItem, dbClient, configManager, usenetClient, queueManager),
            DavItem.ItemType.SymlinkRoot =>
                new DatabaseStoreSymlinkCollection(davItem, dbClient, configManager),
            DavItem.ItemType.NzbFile =>
                new DatabaseStoreNzbFile(davItem, dbClient, usenetClient, configManager),
            DavItem.ItemType.RarFile =>
                new DatabaseStoreRarFile(davItem, dbClient, usenetClient, configManager),
            _ => throw new ArgumentException("Unrecognized directory child type.")
        };
    }
}
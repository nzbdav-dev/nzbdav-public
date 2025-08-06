﻿using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav.Base;
using NzbWebDAV.WebDav.Requests;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreSymlinkCollection(
    DavItem davDirectory,
    DavDatabaseClient dbClient,
    ConfigManager configManager,
    IMemoryCache memoryCache,
    string parentPath = ""
) : BaseStoreCollection
{
    public override string Name => davDirectory.Name;
    public override string UniqueKey => davDirectory.Id.ToString();

    private string RelativePath => davDirectory.Id == DavItem.SymlinkFolder.Id ? "" : Path.Join(parentPath, Name);
    private Guid TargetId => davDirectory.Id == DavItem.SymlinkFolder.Id ? DavItem.ContentFolder.Id : davDirectory.Id;
    private DeletedFileManager DeletedFiles => new(davDirectory.Id);
    private string CacheKey => $"symlink_children_{TargetId}";

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        throw new InvalidOperationException("Files and Directories cannot be copied.");
    }

    protected override async Task<IStoreItem?> GetItemAsync(GetItemRequest request)
    {
        if (DeletedFiles.IsDeleted(request.Name)) return null;
        var name = Regex.Replace(request.Name, @"\.rclonelink$", "");
        var child = await dbClient.GetDirectoryChildAsync(TargetId, name, request.CancellationToken);
        if (child is null) return null;
        return GetItem(child);
    }

    protected override async Task<IStoreItem[]> GetAllItemsAsync(CancellationToken cancellationToken)
    {
        return await memoryCache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            
            // For large directories, use pagination to avoid memory issues
            var totalCount = await dbClient.GetDirectoryChildrenCountAsync(TargetId, cancellationToken);
            if (totalCount > 1000)
            {
                var allItems = new List<IStoreItem>();
                const int pageSize = 500;
                
                for (int skip = 0; skip < totalCount; skip += pageSize)
                {
                    var pageItems = await dbClient.GetDirectoryChildrenAsync(TargetId, skip, pageSize, cancellationToken);
                    allItems.AddRange(pageItems.Select(GetItem).Where(x => !DeletedFiles.IsDeleted(x.Name)));
                }
                
                return allItems.ToArray();
            }
            
            return (await dbClient.GetDirectoryChildrenAsync(TargetId, cancellationToken))
                .Select(GetItem)
                .Where(x => !DeletedFiles.IsDeleted(x.Name))
                .ToArray();
        });
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

    protected override Task<DavStatusCode> DeleteItemAsync(DeleteItemRequest request)
    {
        // Items cannot be deleted from the '/completed-symlinks' folder.
        // This path simply mirrors the '/content' folder, except with symlinks.
        // This allows radarr/sonarr to import the lightweight symlink, instead
        // of trying to import large-sized media.
        //
        // However, when radarr attempts to import the symlink, it does so by moving
        // it to the media library. But since the symlinks lives in a separate
        // file-system (rclone-mounted webdav), the operating system will instead
        // perform a copy-and-delete operation. For the import to succeed, we must
        // trick the OS into thinking that the "delete" worked.
        //
        // The symlink doesn't actually exist anywhere. It takes zero storage and
        // just gets created in memory, as needed, for webdav requests. The only
        // thing that exists is the underlying data within the '/content' directory
        // But in this request, we only want to "delete" the symlink. We don't want
        // to delete the underlying media within the '/content' directory.
        //
        // Instead, we store the filename in a temporary cache (for 30 seconds).
        // While the filename is in the cache, we will no longer create that
        // symlink on-the-fly in subsequent webdav requests. It essentially
        // mimics a deletion even though there was nothing to delete in the first
        // place, since everything is created on the fly, mirroring the '/content'
        // directory.
        //
        // (204 No Content) is the correct status code to return for a successful
        // deletion of a file. This status code means the server has successfully
        // processed the request, and there is no additional content to send in the
        // response body. (200 OK) is also acceptable, but more appropriate for when
        // the server also returns a response body with the status of the operation.
        DeletedFiles.AddDeletedFile(request.Name, TimeSpan.FromSeconds(30));
        return Task.FromResult(DavStatusCode.NoContent);
    }

    private IStoreItem GetItem(DavItem davItem)
    {
        return davItem.Type switch
        {
            DavItem.ItemType.Directory =>
                new DatabaseStoreSymlinkCollection(davItem, dbClient, configManager, memoryCache, RelativePath),
            DavItem.ItemType.NzbFile =>
                new DatabaseStoreSymlinkFile(davItem, RelativePath, configManager),
            DavItem.ItemType.RarFile =>
                new DatabaseStoreSymlinkFile(davItem, RelativePath, configManager),
            _ => throw new ArgumentException("Unrecognized directory child type.")
        };
    }

    private class DeletedFileManager(Guid directoryId)
    {
        private static readonly MemoryCache DeletedFiles = new(new MemoryCacheOptions());

        public void AddDeletedFile(string filename, TimeSpan? expiry = null)
        {
            using var entry = DeletedFiles.CreateEntry(GetKey(filename));
            entry.SlidingExpiration = expiry ?? TimeSpan.FromSeconds(30);
            entry.Value = true;
        }

        public bool IsDeleted(string filename)
        {
            return (bool)(DeletedFiles.Get(GetKey(filename)) ?? false);
        }

        private string GetKey(string filename)
        {
            return $"{directoryId}/{filename}";
        }
    }
}
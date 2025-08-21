using Microsoft.AspNetCore.Http;
using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.Clients;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.WebDav.Base;
using NzbWebDAV.WebDav.Requests;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreIdsCollection(
    string name,
    string currentPath,
    HttpContext httpContext,
    DavDatabaseClient dbClient,
    UsenetStreamingClient usenetClient,
    ConfigManager configManager
) : BaseStoreCollection
{
    public override string Name => name;
    public override string UniqueKey => currentPath;
    public override DateTime CreatedAt => default;

    private const int FanningDepth = 5;
    private const string Alphabet = "0123456789abcdef";

    private readonly string[] _currentPathParts = currentPath.Split(
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar,
        StringSplitOptions.RemoveEmptyEntries
    );

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        throw new InvalidOperationException("Files and Directories cannot be copied.");
    }

    protected override async Task<IStoreItem?> GetItemAsync(GetItemRequest request)
    {
        var (dir, ctx, db, usenet, config) = (request.Name, httpContext, dbClient, usenetClient, configManager);
        if (_currentPathParts.Length < FanningDepth)
        {
            if (request.Name.Length != 1) return null;
            if (!Alphabet.Contains(request.Name[0])) return null;
            return new DatabaseStoreIdsCollection(dir, Path.Join(currentPath, dir), ctx, db, usenet, config);
        }

        var item = await dbClient.GetFileById(request.Name);
        return item == null ? null : new DatabaseStoreIdFile(item, ctx, dbClient, usenet, config);
    }

    protected override async Task<IStoreItem[]> GetAllItemsAsync(CancellationToken cancellationToken)
    {
        var (ctx, db, usenet, config) = (httpContext, dbClient, usenetClient, configManager);
        if (_currentPathParts.Length < FanningDepth)
            return Alphabet
                .Select(x => x.ToString())
                .Select(x => new DatabaseStoreIdsCollection(x, Path.Join(currentPath, x), ctx, db, usenet, config))
                .Select(x => x as IStoreItem)
                .ToArray();

        var idPrefix = string.Join("", _currentPathParts);
        return (await dbClient.GetFilesByIdPrefix(idPrefix))
            .Select(x => new DatabaseStoreIdFile(x, ctx, db, usenet, config))
            .Select(x => x as IStoreItem)
            .ToArray();
    }

    protected override Task<StoreItemResult> CreateItemAsync(CreateItemRequest request)
    {
        throw new InvalidOperationException("Files cannot be created.");
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
        throw new InvalidOperationException("Files and Directories cannot be deleted.");
    }
}
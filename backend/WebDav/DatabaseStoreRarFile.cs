using Microsoft.EntityFrameworkCore;
using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.Clients;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Streams;
using NzbWebDAV.WebDav.Base;
using NzbWebDAV.WebDav.Requests;
using Microsoft.Extensions.Logging;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreRarFile(
    DavItem davRarFile,
    DavDatabaseClient dbClient,
    UsenetProviderManager usenetClient,
    ConfigManager configManager,
    ILogger<DatabaseStoreRarFile> logger
) : BaseStoreItem
{
    public override string Name => davRarFile.Name;
    public override string UniqueKey => davRarFile.Id.ToString();
    public override long FileSize => davRarFile.FileSize!.Value;

    public override async Task<Stream> GetReadableStreamAsync(CancellationToken ct)
    {
        var id = davRarFile.Id;
        var rarFile = await dbClient.Ctx.RarFiles.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (rarFile is null) throw new FileNotFoundException($"Could not find nzb file with id: {id}");
        return new RarFileStream(rarFile.RarParts, usenetClient, configManager.GetConnectionsPerStream());
    }

    protected override Task<DavStatusCode> UploadFromStreamAsync(UploadFromStreamRequest request)
    {
        logger.LogError("Attempted to modify read-only RAR file '{FileName}'", davRarFile.Name);
        return Task.FromResult(DavStatusCode.Forbidden);
    }

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        logger.LogError("Attempted to copy read-only RAR file '{FileName}'", davRarFile.Name);
        return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));
    }
}
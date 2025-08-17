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
using Serilog;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreRarFile(
    DavItem davRarFile,
    DavDatabaseClient dbClient,
    UsenetStreamingClient usenetClient,
    ConfigManager configManager
) : BaseStoreItem
{
    public override string Name => davRarFile.Name;
    public override string UniqueKey => davRarFile.Id.ToString();
    public override long FileSize => davRarFile.FileSize!.Value;
    public override DateTime CreatedAt => davRarFile.CreatedAt;

    public override async Task<Stream> GetReadableStreamAsync(CancellationToken ct)
    {
        var id = davRarFile.Id;
        var rarFile = await dbClient.Ctx.RarFiles.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (rarFile is null) throw new FileNotFoundException($"Could not find nzb file with id: {id}");
        return new RarFileStream(rarFile.RarParts, usenetClient, configManager.GetConnectionsPerStream());
    }

    protected override Task<DavStatusCode> UploadFromStreamAsync(UploadFromStreamRequest request)
    {
        Log.Error("nzb-mounted files cannot be modified.");
        return Task.FromResult(DavStatusCode.Forbidden);
    }

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        Log.Error("nzb-mounted files cannot be copied.");
        return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));
    }
}
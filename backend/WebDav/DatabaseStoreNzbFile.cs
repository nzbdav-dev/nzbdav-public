using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.Clients;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav.Base;
using NzbWebDAV.WebDav.Requests;
using Microsoft.Extensions.Logging;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreNzbFile(
    DavItem davNzbFile,
    DavDatabaseClient dbClient,
    UsenetProviderManager usenetClient,
    ConfigManager configManager,
    ILogger<DatabaseStoreNzbFile> logger
) : BaseStoreItem
{
    public override string Name => davNzbFile.Name;
    public override string UniqueKey => davNzbFile.Id.ToString();
    public override long FileSize => davNzbFile.FileSize!.Value;

    public override async Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        var id = davNzbFile.Id;
        var file = await dbClient.GetNzbFileAsync(id, cancellationToken);
        if (file is null) throw new FileNotFoundException($"Could not find nzb file with id: {id}");
        return usenetClient.GetFileStream(file.SegmentIds, FileSize, configManager.GetConnectionsPerStream());
    }

    protected override Task<DavStatusCode> UploadFromStreamAsync(UploadFromStreamRequest request)
    {
        logger.LogError("Attempted to modify read-only NZB file '{FileName}'", davNzbFile.Name);
        return Task.FromResult(DavStatusCode.Forbidden);
    }

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        logger.LogError("Attempted to copy read-only NZB file '{FileName}'", davNzbFile.Name);
        return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));
    }
}
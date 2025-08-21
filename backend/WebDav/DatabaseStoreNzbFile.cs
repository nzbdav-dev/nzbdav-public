using Microsoft.AspNetCore.Http;
using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.Clients;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav.Base;
using NzbWebDAV.WebDav.Requests;
using Serilog;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreNzbFile(
    DavItem davNzbFile,
    HttpContext httpContext,
    DavDatabaseClient dbClient,
    UsenetStreamingClient usenetClient,
    ConfigManager configManager
) : BaseStoreItem
{
    public override string Name => davNzbFile.Name;
    public override string UniqueKey => davNzbFile.Id.ToString();
    public override long FileSize => davNzbFile.FileSize!.Value;
    public override DateTime CreatedAt => davNzbFile.CreatedAt;

    public override async Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        // store the DavItem being accessed in the http context
        httpContext.Items["DavItem"] = davNzbFile;

        // return the stream
        var id = davNzbFile.Id;
        var file = await dbClient.GetNzbFileAsync(id, cancellationToken);
        if (file is null) throw new FileNotFoundException($"Could not find nzb file with id: {id}");
        return usenetClient.GetFileStream(file.SegmentIds, FileSize, configManager.GetConnectionsPerStream());
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
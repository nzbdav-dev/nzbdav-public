using System.Text;
using Microsoft.EntityFrameworkCore;
using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav.Base;
using NzbWebDAV.WebDav.Requests;
using Serilog;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreQueueItem(
    QueueItem queueItem,
    DavDatabaseClient dbClient
) : BaseStoreItem
{
    public QueueItem QueueItem => queueItem;
    public override string Name => queueItem.FileName;
    public override string UniqueKey => queueItem.Id.ToString();
    public override long FileSize => queueItem.NzbFileSize;
    public override DateTime CreatedAt => queueItem.CreatedAt;

    public override async Task<Stream> GetReadableStreamAsync(CancellationToken ct)
    {
        var id = queueItem.Id;
        var document = await dbClient.Ctx.QueueItems.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
        if (document is null) throw new FileNotFoundException($"Could not find nzb document with id: {id}");
        return new MemoryStream(Encoding.UTF8.GetBytes(document.NzbContents));
    }

    protected override Task<DavStatusCode> UploadFromStreamAsync(UploadFromStreamRequest request)
    {
        Log.Error("Nzb document files cannot be modified.");
        return Task.FromResult(DavStatusCode.Forbidden);
    }

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        throw new InvalidOperationException("Nzb document files cannot be copied.");
    }
}
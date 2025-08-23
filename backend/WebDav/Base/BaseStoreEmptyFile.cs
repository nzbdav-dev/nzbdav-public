using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.WebDav.Requests;

namespace NzbWebDAV.WebDav.Base;

public class BaseStoreEmptyFile(string name) : BaseStoreItem
{
    public override string Name => name;
    public override string UniqueKey { get; } = Guid.NewGuid().ToString();
    public override long FileSize => 0;
    public override DateTime CreatedAt { get; } = DateTime.Now;

    public override Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Stream>(new MemoryStream([]));
    }

    protected override Task<DavStatusCode> UploadFromStreamAsync(UploadFromStreamRequest request)
    {
        return Task.FromResult(DavStatusCode.Forbidden);
    }

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        throw new InvalidOperationException("this file cannot be copied.");
    }
}
using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.WebDav.Requests;
using Serilog;

namespace NzbWebDAV.WebDav.Base;

public abstract class BaseStoreReadonlyItem : BaseStoreItem
{
    protected override Task<DavStatusCode> UploadFromStreamAsync(UploadFromStreamRequest request)
    {
        Log.Warning($"Cannot upload item `{Name}`: Forbidden");
        return Task.FromResult(DavStatusCode.Forbidden);
    }

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        Log.Warning($"Cannot copy item `{request.Name}`: Forbidden.");
        return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));
    }
}
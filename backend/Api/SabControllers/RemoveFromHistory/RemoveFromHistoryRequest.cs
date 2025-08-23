using Microsoft.AspNetCore.Http;
using NzbWebDAV.Extensions;

namespace NzbWebDAV.Api.SabControllers.RemoveFromHistory;

public class RemoveFromHistoryRequest()
{
    public string NzoId { get; init; }
    public bool DeleteCompletedFiles { get; init; }
    public CancellationToken CancellationToken { get; init; }

    public RemoveFromHistoryRequest(HttpContext httpContext) : this()
    {
        // Note: The official SABnzbd api has a query param named `del_files`
        // which only applies to Failed jobs and does not apply to Completed jobs.
        // However, Failed jobs in nzbdav never add anything to the webdav anyway,
        // so there is never anything to delete for Failed jobs. For this reason,
        // the `del_files` query param from SABnzbd is not needed here at all.
        //
        // Instead, a non-standard `del_completed_files` query param is added.
        // It applies to Completed jobs and does not apply to Failed jobs. It is
        // only used by the nzbdav web-ui when manually clearing History items to
        // provide users the option to delete all related files.
        NzoId = httpContext.GetQueryParam("value")!;
        DeleteCompletedFiles = httpContext.GetQueryParam("del_completed_files") == "1";
        CancellationToken = httpContext.RequestAborted;
    }
}
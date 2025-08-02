using Microsoft.AspNetCore.Http;
using NzbWebDAV.Extensions;

namespace NzbWebDAV.Api.SabControllers.RemoveFromHistory;

public class RemoveFromHistoryRequest()
{
    public string NzoId { get; init; } = string.Empty;
    public CancellationToken CancellationToken { get; init; }

    public RemoveFromHistoryRequest(HttpContext httpContext): this()
    {
        NzoId = httpContext.GetQueryParam("value")!;
        CancellationToken = httpContext.RequestAborted;
    }
}
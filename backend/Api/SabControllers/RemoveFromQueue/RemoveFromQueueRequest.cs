using Microsoft.AspNetCore.Http;
using NzbWebDAV.Extensions;

namespace NzbWebDAV.Api.SabControllers.RemoveFromQueue;

public class RemoveFromQueueRequest()
{
    public string NzoId { get; init; } = string.Empty;
    public CancellationToken CancellationToken { get; init; }

    public RemoveFromQueueRequest(HttpContext httpContext): this()
    {
        NzoId = httpContext.GetQueryParam("value")!;
        CancellationToken = httpContext.RequestAborted;
    }
}
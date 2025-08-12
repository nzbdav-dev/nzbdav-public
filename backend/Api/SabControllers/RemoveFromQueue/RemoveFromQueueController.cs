using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Queue;

namespace NzbWebDAV.Api.SabControllers.RemoveFromQueue;

public class RemoveFromQueueController(
    HttpContext httpContext,
    DavDatabaseClient dbClient,
    QueueManager queueManager,
    ConfigManager configManager
) : SabApiController.BaseController(httpContext, configManager)
{
    public async Task<RemoveFromQueueResponse> RemoveFromQueue(RemoveFromQueueRequest request)
    {
        await queueManager.RemoveQueueItemAsync(request.NzoId, dbClient);
        return new RemoveFromQueueResponse() { Status = true };
    }

    protected override async Task<IActionResult> Handle()
    {
        var request = new RemoveFromQueueRequest(httpContext);
        return Ok(await RemoveFromQueue(request));
    }
}
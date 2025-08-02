using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Services;

namespace NzbWebDAV.Api.SabControllers.ClearQueue;

public class ClearQueueController(
    HttpContext httpContext,
    DavDatabaseClient dbClient,
    ConfigManager configManager,
    QueueManager queueManager
) : SabApiController.BaseController(httpContext, configManager)
{
    protected override bool RequiresAuthentication => true;

    protected override async Task<IActionResult> Handle()
    {
        try
        {
            var totalRemovedCount = 0;

            // 1. Cancel any currently processing queue item
            var (currentItem, _) = queueManager.GetInProgressQueueItem();
            if (currentItem != null)
            {
                await queueManager.RemoveQueueItemAsync(currentItem.Id.ToString(), dbClient);
                totalRemovedCount++;
            }

            // 2. Remove all pending queue items
            var queueItems = dbClient.Ctx.QueueItems.ToList();
            var queueCount = queueItems.Count;
            if (queueCount > 0)
            {
                dbClient.Ctx.QueueItems.RemoveRange(queueItems);
                totalRemovedCount += queueCount;
            }

            await dbClient.Ctx.SaveChangesAsync();

            return Ok(new
            {
                status = true,
                nzo_ids = Array.Empty<string>(),
                removed_count = totalRemovedCount
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { status = false, error = ex.Message });
        }
    }
}
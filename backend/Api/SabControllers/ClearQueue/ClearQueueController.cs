using Microsoft.AspNetCore.Http;
using NzbWebDAV.Config;
using NzbWebDAV.Database;

namespace NzbWebDAV.Api.SabControllers.ClearQueue;

public class ClearQueueController(
    HttpContext httpContext,
    DavDatabaseClient dbClient,
    ConfigManager configManager
) : SabApiController.BaseController(httpContext, configManager)
{
    protected override bool RequiresAuthentication => true;

    protected override async Task<object> HandleRequestAsync()
    {
        try
        {
            // Remove all items from the queue
            var queueItems = dbClient.Ctx.QueueItems.ToList();
            var removedCount = queueItems.Count;
            
            if (removedCount > 0)
            {
                dbClient.Ctx.QueueItems.RemoveRange(queueItems);
                await dbClient.Ctx.SaveChangesAsync();
            }

            return new
            {
                status = true,
                nzo_ids = Array.Empty<string>(),
                removed_count = removedCount
            };
        }
        catch (Exception ex)
        {
            return new { status = false, error = ex.Message };
        }
    }
}
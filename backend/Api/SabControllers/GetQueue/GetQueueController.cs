using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Queue;

namespace NzbWebDAV.Api.SabControllers.GetQueue;

public class GetQueueController(
    HttpContext httpContext,
    DavDatabaseClient dbClient,
    QueueManager queueManager,
    ConfigManager configManager
) : SabApiController.BaseController(httpContext, configManager)
{
    private async Task<GetQueueResponse> GetQueueAsync(GetQueueRequest request)
    {
        // get in progress item
        var (inProgressQueueItem, progressPercentage) = queueManager.GetInProgressQueueItem();

        // get queued items
        var ct = request.CancellationToken;
        var queueItems = (await dbClient.GetQueueItems(request.Category, request.Start, request.Limit, ct))
            .Where(x => x.Id != inProgressQueueItem?.Id)
            .ToArray();

        // get slots
        var slots = queueItems
            .Prepend(inProgressQueueItem)
            .Where(queueItem => queueItem != null)
            .Select((queueItem, index) =>
            {
                var percentage = (queueItem == inProgressQueueItem ? progressPercentage : 0)!.Value;
                return new GetQueueResponse.QueueSlot
                {
                    Index = index,
                    NzoId = queueItem!.Id.ToString(),
                    Priority = queueItem.Priority.ToString(),
                    Filename = queueItem.FileName,
                    Category = queueItem.Category,
                    Percentage = percentage.ToString()!,
                    Status = queueItem == inProgressQueueItem ? "Downloading" : "Queued",
                    TimeLeft = TimeSpan.Zero,
                    SizeInMB = FormatSizeMB(queueItem.TotalSegmentBytes),
                    SizeLeftInMB = FormatSizeMB((100 - percentage) * queueItem.TotalSegmentBytes / 100),
                };
            })
            .ToList();

        // return response
        return new GetQueueResponse()
        {
            Queue = new GetQueueResponse.QueueObject()
            {
                Paused = false,
                Slots = slots,
            }
        };
    }

    private static string FormatSizeMB(long bytes)
    {
        var megabytes = bytes / (1024.0 * 1024.0);
        return megabytes.ToString("0.00");
    }

    protected override async Task<IActionResult> Handle()
    {
        var request = new GetQueueRequest(httpContext);
        return Ok(await GetQueueAsync(request));
    }
}
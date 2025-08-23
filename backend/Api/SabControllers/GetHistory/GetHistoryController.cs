using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;

namespace NzbWebDAV.Api.SabControllers.GetHistory;

public class GetHistoryController(
    HttpContext httpContext,
    DavDatabaseClient dbClient,
    ConfigManager configManager
) : SabApiController.BaseController(httpContext, configManager)
{
    private async Task<GetHistoryResponse> GetHistoryAsync(GetHistoryRequest request)
    {
        // get history items
        var historyItems = await dbClient.Ctx.HistoryItems
            .Where(q => q.Category == request.Category || request.Category == null)
            .OrderByDescending(q => q.CreatedAt)
            .Skip(request.Start)
            .Take(request.Limit)
            .ToArrayAsync(request.CancellationToken);

        // get slots
        var slots = historyItems
            .Select(historyItem => new GetHistoryResponse.HistorySlot()
            {
                NzoId = historyItem.Id.ToString(),
                NzbName = historyItem.FileName,
                JobName = historyItem.JobName,
                Category = historyItem.Category,
                Status = historyItem.DownloadStatus,
                SizeInBytes = historyItem.TotalSegmentBytes,
                DownloadPath = Path.Join(new[]
                {
                    configManager.GetRcloneMountDir(),
                    DavItem.SymlinkFolder.Name,
                    historyItem.Category,
                    historyItem.JobName
                }),
                DownloadTimeSeconds = historyItem.DownloadTimeSeconds,
                FailMessage = historyItem.FailMessage ?? "",
            })
            .ToList();

        // return response
        return new GetHistoryResponse()
        {
            History = new GetHistoryResponse.HistoryObject()
            {
                Slots = slots,
            }
        };
    }

    protected override async Task<IActionResult> Handle()
    {
        var request = new GetHistoryRequest(httpContext);
        return Ok(await GetHistoryAsync(request));
    }
}
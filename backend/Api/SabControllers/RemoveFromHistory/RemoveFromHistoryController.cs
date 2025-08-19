using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Extensions;

namespace NzbWebDAV.Api.SabControllers.RemoveFromHistory;

public class RemoveFromHistoryController(
    HttpContext httpContext,
    DavDatabaseClient dbClient,
    ConfigManager configManager
) : SabApiController.BaseController(httpContext, configManager)
{
    public async Task<RemoveFromHistoryResponse> RemoveFromHistory(RemoveFromHistoryRequest request)
    {
        await using var transaction = await dbClient.Ctx.Database.BeginTransactionAsync();
        var historyItem = await dbClient.GetHistoryItemAsync(request.NzoId);
        if (historyItem is null) return new RemoveFromHistoryResponse() { Status = true };
        if (request.DeleteCompletedFiles) await DeleteCompletedFiles(historyItem, request.CancellationToken);
        dbClient.Ctx.HistoryItems.Remove(historyItem);
        await dbClient.Ctx.SaveChangesAsync(request.CancellationToken);
        await transaction.CommitAsync(request.CancellationToken);
        return new RemoveFromHistoryResponse() { Status = true };
    }

    public async Task DeleteCompletedFiles(HistoryItem historyItem, CancellationToken ct)
    {
        if (historyItem.DownloadDirId is null) return;
        var downloadDir = await dbClient.Ctx.Items.FirstOrDefaultAsync(x => x.Id == historyItem.DownloadDirId, ct);
        if (downloadDir is null) return;
        if (downloadDir.IsProtected()) return;
        dbClient.Ctx.Items.Remove(downloadDir);
    }

    protected override async Task<IActionResult> Handle()
    {
        var request = new RemoveFromHistoryRequest(httpContext);
        return Ok(await RemoveFromHistory(request));
    }
}
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NWebDav.Server.Stores;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.WebDav;

namespace NzbWebDAV.Api.SabControllers.RemoveFromHistory;

public class RemoveFromHistoryController(
    HttpContext httpContext,
    DavDatabaseClient dbClient,
    ConfigManager configManager,
    DatabaseStore store
) : SabApiController.BaseController(httpContext, configManager)
{
    public async Task<RemoveFromHistoryResponse> RemoveFromHistory(RemoveFromHistoryRequest request)
    {
        await using var transaction = await dbClient.Ctx.Database.BeginTransactionAsync();
        var historyItem = await dbClient.GetHistoryItemAsync(request.NzoId);
        if (historyItem is null) return new RemoveFromHistoryResponse() { Status = true };
        if (request.DeleteCompletedFiles) await DeleteCompletedFiles(historyItem, request.CancellationToken);
        dbClient.Ctx.HistoryItems.Remove(historyItem);
        await dbClient.Ctx.SaveChangesAsync();
        await transaction.CommitAsync();
        return new RemoveFromHistoryResponse() { Status = true };
    }

    public async Task DeleteCompletedFiles(HistoryItem historyItem, CancellationToken cancellationToken)
    {
        var categoryPath = Path.Join(DavItem.ContentFolder.Name, historyItem.Category);
        var item = await store.GetItemAsync(categoryPath, cancellationToken);
        if (item is not IStoreCollection dir) return;
        await dir.DeleteItemAsync(historyItem.JobName, cancellationToken);
    }

    protected override async Task<IActionResult> Handle()
    {
        var request = new RemoveFromHistoryRequest(httpContext);
        return Ok(await RemoveFromHistory(request));
    }
}
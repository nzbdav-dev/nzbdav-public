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
        // NEW: collect multiple IDs from query (SAB passes "value") or from body (nzoIds)
        var rawValue = HttpContext.Request.Query["value"].ToString(); // SAB style: value=ID1,ID2
        var bodyIds = Array.Empty<string>();

        // If we also accept JSON body like { "nzoIds": ["id1","id2"] }
        try
        {
            using var reader = new StreamReader(HttpContext.Request.Body);
            var body = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                var obj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(body);
                if (obj != null && obj.TryGetValue("nzoIds", out var arr) && arr != null)
                    bodyIds = arr;
            }
        }
        catch { /* ignore body parse errors, we fall back to query */ }

        // Merge + normalize
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // SAB-style comma-separated "value"
        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            foreach (var part in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                ids.Add(part);
        }

        // Body-provided nzoIds
        foreach (var id in bodyIds)
            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id);

        // Back-compat: if the old single-param was "nzo_id" or "nzoId"
        var single = HttpContext.Request.Query["nzo_id"].ToString();
        if (string.IsNullOrWhiteSpace(single))
            single = HttpContext.Request.Query["nzoId"].ToString();
        if (!string.IsNullOrWhiteSpace(single))
            ids.Add(single);

        if (ids.Count == 0)
            return BadRequest("No nzo_id(s) provided.");

        // ---- ONE database operation: bulk delete
        // Assuming EF Core and a DbContext like _db with a QueueItems DbSet that has NzoId:
        // Here, QueueItem.Id is a Guid, but SAB passes string IDs, so parse as needed
        var guidIds = new List<Guid>();
        foreach (var id in ids)
        {
            if (Guid.TryParse(id, out var guid))
                guidIds.Add(guid);
        }

        var items = await dbClient.Ctx.QueueItems
            .Where(q => guidIds.Contains(q.Id))
            .ToListAsync();

        if (items.Count == 0)
            return Ok(new { removed = 0 });

        dbClient.Ctx.QueueItems.RemoveRange(items);
        await dbClient.Ctx.SaveChangesAsync();  // single transaction

        return Ok(new { removed = items.Count, ids = ids.ToArray() });
    }
}
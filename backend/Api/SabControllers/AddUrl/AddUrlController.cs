using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Api.SabControllers.AddFile;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Queue;

namespace NzbWebDAV.Api.SabControllers.AddUrl;

public class AddUrlController(
    HttpContext httpContext,
    DavDatabaseClient dbClient,
    QueueManager queueManager,
    ConfigManager configManager
) : SabApiController.BaseController(httpContext, configManager)
{
    public async Task<AddUrlResponse> AddUrlAsync(AddUrlRequest request)
    {
        var controller = new AddFileController(httpContext, dbClient, queueManager, configManager);
        var response = await controller.AddFileAsync(request);
        return new AddUrlResponse()
        {
            Status = response.Status,
            NzoIds = response.NzoIds,
        };
    }

    protected override async Task<IActionResult> Handle()
    {
        var request = await AddUrlRequest.New(httpContext);
        return Ok(await AddUrlAsync(request));
    }
}
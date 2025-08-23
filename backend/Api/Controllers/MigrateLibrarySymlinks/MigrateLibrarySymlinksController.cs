using Microsoft.AspNetCore.Mvc;
using NzbWebDAV.Config;
using NzbWebDAV.Tasks;
using NzbWebDAV.WebDav;
using NzbWebDAV.Websocket;

namespace NzbWebDAV.Api.Controllers.MigrateLibrarySymlinks;

[ApiController]
[Route("api/migrate-library-symlinks")]
public class RemoveUnlinkedFilesController(
    ConfigManager configManager,
    WebsocketManager websocketManager,
    DatabaseStore databaseStore
) : BaseApiController
{
    protected override async Task<IActionResult> HandleRequest()
    {
        var task = new MigrateLibrarySymlinksTask(configManager, websocketManager, databaseStore);
        var executed = await task.Execute();
        return Ok(executed);
    }
}
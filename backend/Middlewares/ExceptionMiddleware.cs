using Microsoft.AspNetCore.Http;
using NWebDav.Server.Helpers;
using NzbWebDAV.Exceptions;
using Serilog;

namespace NzbWebDAV.Middlewares;

public class ExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // If the response has not started, we can write our custom response
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = 499; // Non-standard status code for client closed request
                await context.Response.WriteAsync("Client closed request.");
            }
        }
        catch (UsenetArticleNotFoundException e)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = 404;
            }

            Log.Warning($"File `{context.Request.Path}` has missing articles: {e.Message}");
        }
        catch (SeekPositionNotFoundException)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = 404;
            }

            var seekPosition = context.Request.GetRange()?.Start?.ToString() ?? "unknown";
            Log.Warning($"File `{context.Request.Path}` could not seek to byte position: {seekPosition}");
        }
    }
}
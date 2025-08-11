using Microsoft.AspNetCore.Http;
using NWebDav.Server;

namespace NzbWebDAV.Extensions;

public static class NWebDavOptionsExtensions
{
    public static Func<HttpContext, bool> GetFilter(this NWebDavOptions options)
    {
        return context => !context.Request.Path.StartsWithSegments("/api") &&
                          !context.Request.Path.StartsWithSegments("/view") &&
                          !context.Request.Path.StartsWithSegments("/health") &&
                          !context.Request.Path.StartsWithSegments("/ws");
    }
}
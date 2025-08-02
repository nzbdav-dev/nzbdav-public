using Microsoft.AspNetCore.Http;
using Serilog;

namespace NzbWebDAV.Middlewares;

public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;

    public PerformanceMonitoringMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            // Log slow requests (over 1 second)
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                Log.Warning("Slow WebDAV request: {Method} {Path} took {ElapsedMs}ms", 
                    context.Request.Method, 
                    context.Request.Path, 
                    stopwatch.ElapsedMilliseconds);
            }
            
            // Log very slow requests (over 5 seconds)
            if (stopwatch.ElapsedMilliseconds > 5000)
            {
                Log.Error("Very slow WebDAV request: {Method} {Path} took {ElapsedMs}ms", 
                    context.Request.Method, 
                    context.Request.Path, 
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }
} 
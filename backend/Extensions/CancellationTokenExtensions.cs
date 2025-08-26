using System.Collections.Concurrent;

namespace NzbWebDAV.Extensions;

public static class CancellationTokenExtensions
{
    private static readonly ConcurrentDictionary<CancellationToken, object?> Context = new();

    public static CancellationTokenScopedContext SetScopedContext<T>(this CancellationToken ct, T? value)
    {
        Context[ct] = value;
        return new CancellationTokenScopedContext(ct);
    }

    public static T? GetContext<T>(this CancellationToken ct)
    {
        return Context.TryGetValue(ct, out var result) && result is T context ? context : default;
    }

    public class CancellationTokenScopedContext(CancellationToken ct) : IDisposable
    {
        public void Dispose()
        {
            Context.Remove(ct, out _);
        }
    }
}
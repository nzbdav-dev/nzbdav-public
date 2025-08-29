// ReSharper disable InconsistentNaming

namespace NzbWebDAV.Extensions;

public static class IAsyncEnumerableTaskExtensions
{
    public static async Task<List<T>> GetAllAsync<T>
    (
        this IAsyncEnumerable<T> asyncEnumerable,
        CancellationToken ct = default,
        IProgress<int>? progress = null
    )
    {
        var done = 0;
        var results = new List<T>();
        await foreach (var result in asyncEnumerable.WithCancellation(ct))
        {
            results.Add(result);
            progress?.Report(++done);
        }

        return results;
    }
}
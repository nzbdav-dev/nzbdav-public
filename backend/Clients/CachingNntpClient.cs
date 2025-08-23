using Microsoft.Extensions.Caching.Memory;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Clients;

public class CachingNntpClient(INntpClient client, MemoryCache cache) : WrappingNntpClient(client)
{
    private readonly INntpClient _client = client;

    public override async Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken)
    {
        var cacheKey = segmentId;
        return (await cache.GetOrCreateAsync(cacheKey, cacheEntry =>
        {
            cacheEntry.Size = 1;
            cacheEntry.SlidingExpiration = TimeSpan.FromHours(3);
            return _client.GetSegmentYencHeaderAsync(segmentId, cancellationToken);
        })!)!;
    }

    public override async Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken)
    {
        if (file.Segments.Count == 0) return 0;
        var header = await GetSegmentYencHeaderAsync(file.Segments[0].MessageId, cancellationToken);
        return header.FileSize;
    }
}
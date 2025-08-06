using Microsoft.Extensions.Caching.Memory;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Clients;

public class CachingNntpClient(INntpClient client, MemoryCache cache) : WrappingNntpClient(client)
{
    private readonly INntpClient _client = client;
    
    public INntpClient GetInnerClient() => _client;

    public override async Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken)
    {
        var cacheKey = $"yenc_header_{segmentId}";
        return (await cache.GetOrCreateAsync(cacheKey, cacheEntry =>
        {
            cacheEntry.Size = 100; // Increased cache size for headers (small objects)
            cacheEntry.SlidingExpiration = TimeSpan.FromHours(12); // Increased cache time
            cacheEntry.Priority = CacheItemPriority.High; // Headers are frequently accessed
            return _client.GetSegmentYencHeaderAsync(segmentId, cancellationToken);
        })!)!;
    }

    public override async Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken)
    {
        var header = await GetSegmentYencHeaderAsync(file.Segments[0].MessageId, cancellationToken);
        return header.FileSize;
    }
}
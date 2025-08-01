using NzbWebDAV.Streams;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Clients;

public interface IUsenetProvider : IDisposable
{
    /// <summary>
    /// Unique identifier for this provider
    /// </summary>
    string ProviderId { get; }
    
    /// <summary>
    /// Display name for this provider
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Indicates if this provider is currently healthy and available
    /// </summary>
    bool IsHealthy { get; }
    
    /// <summary>
    /// Priority of this provider (lower number = higher priority)
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Host address for this provider
    /// </summary>
    string Host { get; }
    
    /// <summary>
    /// Port number for this provider
    /// </summary>
    int Port { get; }
    
    /// <summary>
    /// Test connection to this provider
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check health of all segments in an NZB file
    /// </summary>
    Task<bool> CheckNzbFileHealthAsync(NzbFile nzbFile, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a stream for downloading an NZB file
    /// </summary>
    Task<NzbFileStream> GetFileStreamAsync(NzbFile nzbFile, int concurrentConnections, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a stream for downloading segments
    /// </summary>
    Stream GetFileStream(string[] segmentIds, long fileSize, int concurrentConnections);
    
    /// <summary>
    /// Get YENC header for a specific segment
    /// </summary>
    Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the size of a file from its segments
    /// </summary>
    Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken = default);
}
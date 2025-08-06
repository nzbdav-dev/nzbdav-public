using NzbWebDAV.Clients;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Extensions;

namespace NzbWebDAV.Streams;

public class RarFileStream(
    DavRarFile.RarPart[] rarParts,
    UsenetProviderManager usenet,
    int concurrentConnections
) : Stream
{
    private long _position = 0;
    private CombinedStream? _innerStream;
    private bool _disposed;
    
    // Cache cumulative offsets for efficient binary search
    private readonly Lazy<long[]> _cumulativeOffsets = new(() =>
    {
        var offsets = new long[rarParts.Length];
        long total = 0;
        for (int i = 0; i < rarParts.Length; i++)
        {
            offsets[i] = total;
            total += rarParts[i].ByteCount;
        }
        return offsets;
    });


    public override void Flush()
    {
        _innerStream?.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_innerStream == null) _innerStream = GetFileStream(_position, cancellationToken);
        var read = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var absoluteOffset = origin == SeekOrigin.Begin ? offset
            : origin == SeekOrigin.Current ? _position + offset
            : throw new InvalidOperationException("SeekOrigin must be Begin or Current.");
        if (_position == absoluteOffset) return _position;
        _position = absoluteOffset;
        _innerStream?.Dispose();
        _innerStream = null;
        return _position;
    }

    public override void SetLength(long value)
    {
        throw new InvalidOperationException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new InvalidOperationException();
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length { get; } = rarParts.Select(x => x.ByteCount).Sum();

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }


    private (int rarPartIndex, long rarPartOffset) SeekRarPart(long byteOffset)
    {
        // Optimized seeking using precomputed cumulative offsets
        if (rarParts.Length <= 10)
        {
            // Use linear search for small arrays (faster due to cache locality)
            var offsets = _cumulativeOffsets.Value;
            for (var i = 0; i < rarParts.Length; i++)
            {
                var nextOffset = offsets[i] + rarParts[i].ByteCount;
                if (byteOffset < nextOffset)
                    return (i, offsets[i]);
            }
        }
        else
        {
            // Efficient binary search using precomputed cumulative offsets
            var offsets = _cumulativeOffsets.Value;
            int left = 0, right = rarParts.Length - 1;
            
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                long partStart = offsets[mid];
                long partEnd = partStart + rarParts[mid].ByteCount;
                
                if (byteOffset < partStart)
                    right = mid - 1;
                else if (byteOffset >= partEnd)
                    left = mid + 1;
                else
                    return (mid, partStart);
            }
        }

        throw new ArgumentOutOfRangeException(nameof(byteOffset));
    }

    private CombinedStream GetFileStream(long rangeStart, CancellationToken cancellationToken)
    {
        if (rangeStart == 0) return GetCombinedStream(0, 0, cancellationToken);
        var (rarPartIndex, rarPartOffset) = SeekRarPart(rangeStart);
        var stream = GetCombinedStream(rarPartIndex, rangeStart - rarPartOffset, cancellationToken);
        return stream;
    }

    private CombinedStream GetCombinedStream(int firstRarPartIndex, long additionalOffset, CancellationToken ct)
    {
        var streams = rarParts[firstRarPartIndex..]
            .Select((x, i) =>
            {
                var offset = (i == 0) ? additionalOffset : 0;
                var stream = usenet.GetFileStream(x.SegmentIds, x.PartSize, concurrentConnections);
                stream.Seek(x.Offset + offset, SeekOrigin.Begin);
                return Task.FromResult(stream.LimitLength(x.ByteCount - offset));
            });
        return new CombinedStream(streams);
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _innerStream?.Dispose();
        _disposed = true;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (_innerStream != null) await _innerStream.DisposeAsync();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
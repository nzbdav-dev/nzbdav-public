using NzbWebDAV.Clients.Connections;
using NzbWebDAV.Streams;
using Usenet.Nntp.Responses;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Clients;

public class MultiConnectionNntpClient(ConnectionPool<INntpClient> connectionPool) : INntpClient
{
    private ConnectionPool<INntpClient> _connectionPool = connectionPool;

    public Task<bool> ConnectAsync(string host, int port, bool useSsl, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Please connect within the connectionFactory");
    }

    public Task<bool> AuthenticateAsync(string user, string pass, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Please authenticate within the connectionFactory");
    }

    public Task<NntpStatResponse> StatAsync(string segmentId, CancellationToken cancellationToken)
    {
        return RunWithConnection(connection => connection.StatAsync(segmentId, cancellationToken), cancellationToken);
    }

    public Task<NntpDateResponse> DateAsync(CancellationToken cancellationToken)
    {
        return RunWithConnection(connection => connection.DateAsync(cancellationToken), cancellationToken);
    }

    public Task<YencHeaderStream> GetSegmentStreamAsync(string segmentId, CancellationToken cancellationToken)
    {
        return RunWithConnection(connection => connection.GetSegmentStreamAsync(segmentId, cancellationToken), cancellationToken);
    }

    public Task<YencHeader> GetSegmentYencHeaderAsync(string segmentId, CancellationToken cancellationToken)
    {
        return RunWithConnection(connection => connection.GetSegmentYencHeaderAsync(segmentId, cancellationToken), cancellationToken);
    }

    public Task<long> GetFileSizeAsync(NzbFile file, CancellationToken cancellationToken)
    {
        return RunWithConnection(connection => connection.GetFileSizeAsync(file, cancellationToken), cancellationToken);
    }

    public async Task WaitForReady(CancellationToken cancellationToken)
    {
        using var connectionLock = await _connectionPool.GetConnectionLockAsync(cancellationToken);
    }

    private async Task<T> RunWithConnection<T>(Func<INntpClient, Task<T>> task, CancellationToken cancellationToken)
    {
        var connectionLock = await _connectionPool.GetConnectionLockAsync(cancellationToken);
        try
        {
            var result = await task(connectionLock.Connection);

            // we only want to release the connection-lock once the underlying connection is ready again.
            // ReSharper disable once MethodSupportsCancellation
            // we intentionally do not pass the cancellation token to ContinueWith,
            // since we want the continuation to always run.
            _ = connectionLock.Connection.WaitForReady(CancellationToken.None).ContinueWith(_ => connectionLock.Dispose());
            return result;
        }
        catch (Exception)
        {
            // we also want to release the connection-lock if there was any error getting the result.
            connectionLock.Dispose();
            throw;
        }
    }

    public void UpdateConnectionPool(ConnectionPool<INntpClient> connectionPool)
    {
        var oldConnectionPool = _connectionPool;
        _connectionPool = connectionPool;
        oldConnectionPool.Dispose();
    }

    public int GetActiveConnectionCount() => _connectionPool.ActiveConnectionCount;
    
    public int GetAvailableConnectionCount() => _connectionPool.AvailableConnectionCount;

    public void Dispose()
    {
        _connectionPool.Dispose();
        GC.SuppressFinalize(this);
    }
}
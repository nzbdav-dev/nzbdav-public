namespace NzbWebDAV.Clients.Connections;

public readonly struct ReservedConnectionsContext(int reservedCount)
{
    public int Count => reservedCount;
}
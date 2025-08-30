namespace NzbWebDAV.WebDav.Base;

public class BaseStoreEmptyFile(string name) : BaseStoreReadonlyItem
{
    public override string Name => name;
    public override string UniqueKey { get; } = Guid.NewGuid().ToString();
    public override long FileSize => 0;
    public override DateTime CreatedAt { get; } = DateTime.Now;

    public override Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Stream>(new MemoryStream([]));
    }
}
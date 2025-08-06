using System.Text;
using NWebDav.Server;
using NWebDav.Server.Stores;
using NzbWebDAV.Config;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Utils;
using NzbWebDAV.WebDav.Base;
using NzbWebDAV.WebDav.Requests;
using Microsoft.Extensions.Logging;

namespace NzbWebDAV.WebDav;

public class DatabaseStoreSymlinkFile(DavItem davFile, string parentPath, ConfigManager configManager, ILogger<DatabaseStoreSymlinkFile> logger) : BaseStoreItem
{
    public override string Name => davFile.Name + ".rclonelink";
    public override string UniqueKey => davFile.Id + ".rclonelink";
    public override long FileSize => ContentBytes.Length;

    private string TargetPath =>
        Path.Combine(configManager.GetRcloneMountDir(), DavItem.ContentFolder.Name, parentPath, davFile.Name);

    private byte[] ContentBytes =>
        Encoding.UTF8.GetBytes(TargetPath);

    public override Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<Stream>(new MemoryStream(ContentBytes));
    }

    protected override Task<DavStatusCode> UploadFromStreamAsync(UploadFromStreamRequest request)
    {
        logger.LogError("Attempted to modify read-only symlink file '{FileName}' (mirrors /content)", davFile.Name);
        return Task.FromResult(DavStatusCode.Forbidden);
    }

    protected override Task<StoreItemResult> CopyAsync(CopyRequest request)
    {
        logger.LogError("Attempted to copy read-only symlink file '{FileName}' (mirrors /content)", davFile.Name);
        return Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));
    }
}
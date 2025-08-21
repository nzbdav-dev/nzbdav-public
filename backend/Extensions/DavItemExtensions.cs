using NzbWebDAV.Database.Models;

namespace NzbWebDAV.Extensions;

public static class DavItemExtensions
{
    private static readonly HashSet<Guid> Protected =
    [
        DavItem.Root.Id,
        DavItem.SymlinkFolder.Id,
        DavItem.ContentFolder.Id,
        DavItem.NzbFolder.Id,
        DavItem.IdsFolder.Id,
    ];

    public static bool IsProtected(this DavItem item)
    {
        return Protected.Contains(item.Id);
    }
}
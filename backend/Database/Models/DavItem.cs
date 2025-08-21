using System.Text.Json.Serialization;

namespace NzbWebDAV.Database.Models;

public class DavItem
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid? ParentId { get; init; }
    public string Name { get; init; } = null!;
    public long? FileSize { get; set; }
    public ItemType Type { get; init; }

    // Important: numerical values cannot be
    // changed without a database migration.
    public enum ItemType
    {
        Directory = 1,
        SymlinkRoot = 2,
        NzbFile = 3,
        RarFile = 4,
        IdsRoot = 5,
    }

    // navigation helpers
    [JsonIgnore]
    public DavItem? Parent { get; set; }

    [JsonIgnore]
    public ICollection<DavItem> Children { get; set; } = new List<DavItem>();

    // static instances
    // Important: assigned values cannot be
    // changed without a database migration.
    public static readonly DavItem Root = new()
    {
        Id = Guid.Parse("00000000-0000-0000-0000-000000000000"),
        ParentId = null,
        Name = "/",
        FileSize = null,
        Type = ItemType.Directory,
    };

    public static readonly DavItem NzbFolder = new()
    {
        Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
        ParentId = Root.Id,
        Name = "nzbs",
        FileSize = null,
        Type = ItemType.Directory,
    };

    public static readonly DavItem ContentFolder = new()
    {
        Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
        ParentId = Root.Id,
        Name = "content",
        FileSize = null,
        Type = ItemType.Directory,
    };

    public static readonly DavItem SymlinkFolder = new()
    {
        Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
        ParentId = Root.Id,
        Name = "completed-symlinks",
        FileSize = null,
        Type = ItemType.SymlinkRoot,
    };

    public static readonly DavItem IdsFolder = new()
    {
        Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
        ParentId = Root.Id,
        Name = ".ids",
        FileSize = null,
        Type = ItemType.IdsRoot,
    };
}
using System.Xml.Linq;
using NWebDav.Server;
using NWebDav.Server.Props;

namespace NzbWebDAV.WebDav.Base;

public class BaseStoreCollectionPropertyManager() : PropertyManager<BaseStoreCollection>(DavProperties)
{
    private static readonly XElement DavResourceType = new(WebDavNamespaces.DavNs + "collection");

    private static readonly DavProperty<BaseStoreCollection>[] DavProperties =
    [
        new DavDisplayName<BaseStoreCollection>
        {
            Getter = collection => collection.Name
        },
        new DavGetResourceType<BaseStoreCollection>
        {
            Getter = _ => [DavResourceType]
        },
        new DavGetLastModified<BaseStoreCollection>
        {
            Getter = x => x.CreatedAt
        },
        new Win32FileAttributes<BaseStoreCollection>
        {
            Getter = _ => FileAttributes.Directory
        },
        new DavQuotaAvailableBytes<BaseStoreCollection>()
        {
            Getter = _ => long.MaxValue
        },
        new DavQuotaUsedBytes<BaseStoreCollection>()
        {
            Getter = _ => 0
        }
    ];

    public static readonly BaseStoreCollectionPropertyManager Instance = new();
}
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Queue.FileProcessors;

namespace NzbWebDAV.Queue.FileAggregators;

public class RarAggregator(DavDatabaseClient dbClient, DavItem mountDirectory) : IAggregator
{
    public void UpdateDatabase(List<BaseProcessor.Result> processorResults)
    {
        var orderedArchiveParts = processorResults
            .OfType<RarProcessor.Result>()
            .OrderBy(x => x.PartNumber)
            .ToList();

        ProcessArchive(orderedArchiveParts);
    }

    private void ProcessArchive(List<RarProcessor.Result> archiveParts)
    {
        var archiveFiles = new Dictionary<string, List<DavRarFile.RarPart>>();
        foreach (var archivePart in archiveParts)
        {
            foreach (var fileSegment in archivePart.StoredFileSegments)
            {
                if (!archiveFiles.ContainsKey(fileSegment.PathWithinArchive))
                    archiveFiles.Add(fileSegment.PathWithinArchive, new List<DavRarFile.RarPart>());

                archiveFiles[fileSegment.PathWithinArchive].Add(new DavRarFile.RarPart()
                {
                    SegmentIds = archivePart.NzbFile.Segments.Select(x => x.MessageId.Value).ToArray(),
                    PartSize = archivePart.PartSize,
                    Offset = fileSegment.Offset,
                    ByteCount = fileSegment.ByteCount,
                });
            }
        }

        foreach (var archiveFile in archiveFiles)
        {
            var pathWithinArchive = archiveFile.Key;
            var rarParts = archiveFile.Value.ToArray();
            var parentDirectory = EnsurePath(pathWithinArchive);

            var davItem = new DavItem()
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.Now,
                ParentId = parentDirectory.Id,
                Name = Path.GetFileName(pathWithinArchive),
                FileSize = rarParts.Sum(x => x.ByteCount),
                Type = DavItem.ItemType.RarFile
            };

            var davRarFile = new DavRarFile()
            {
                Id = davItem.Id,
                RarParts = rarParts,
            };

            dbClient.Ctx.Items.Add(davItem);
            dbClient.Ctx.RarFiles.Add(davRarFile);
        }
    }

    private DavItem EnsurePath(string pathWithinArchive)
    {
        var pathSegments = pathWithinArchive.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parentDirectory = mountDirectory;
        var pathKey = "";
        for (var i = 0; i < pathSegments.Length - 1; i++)
        {
            pathKey = Path.Join(pathKey, pathSegments[i]);
            parentDirectory = EnsureDirectory(parentDirectory, pathSegments[i], pathKey);
        }

        return parentDirectory;
    }

    private readonly Dictionary<string, DavItem> _directoryCache = new();

    private DavItem EnsureDirectory(DavItem parentDirectory, string directoryName, string pathKey)
    {
        if (_directoryCache.TryGetValue(pathKey, out var cachedDirectory)) return cachedDirectory;
        var directory = new DavItem()
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.Now,
            ParentId = parentDirectory.Id,
            Name = directoryName,
            Type = DavItem.ItemType.Directory
        };
        _directoryCache.Add(pathKey, directory);
        dbClient.Ctx.Items.Add(directory);
        return directory;
    }
}
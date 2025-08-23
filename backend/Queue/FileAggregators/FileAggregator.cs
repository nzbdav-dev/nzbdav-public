using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Queue.FileProcessors;

namespace NzbWebDAV.Queue.FileAggregators;

public class FileAggregator(DavDatabaseClient dbClient, DavItem mountDirectory) : IAggregator
{
    public void UpdateDatabase(List<BaseProcessor.Result> processorResults)
    {
        foreach (var processorResult in processorResults)
        {
            if (processorResult is not FileProcessor.Result result) continue;
            if (result.FileName == "") continue; // skip files whose name we can't determine

            var davItem = new DavItem()
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.Now,
                ParentId = mountDirectory.Id,
                Name = result.FileName,
                FileSize = result.FileSize,
                Type = DavItem.ItemType.NzbFile,
                Path = Path.Join(mountDirectory.Path, result.FileName),
            };

            var davNzbFile = new DavNzbFile()
            {
                Id = davItem.Id,
                SegmentIds = result.NzbFile.Segments.Select(x => x.MessageId.Value).ToArray(),
            };

            dbClient.Ctx.Items.Add(davItem);
            dbClient.Ctx.NzbFiles.Add(davNzbFile);
        }
    }
}
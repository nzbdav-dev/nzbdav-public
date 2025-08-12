using System.Text;
using Microsoft.EntityFrameworkCore;
using NzbWebDAV.Clients;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Extensions;
using NzbWebDAV.Queue.FileAggregators;
using NzbWebDAV.Queue.FileProcessors;
using NzbWebDAV.Queue.Validators;
using NzbWebDAV.Utils;
using Serilog;
using Usenet.Nzb;

namespace NzbWebDAV.Queue;

public class QueueItemProcessor(
    QueueItem queueItem,
    DavDatabaseClient dbClient,
    UsenetStreamingClient usenetClient,
    ConfigManager configManager,
    IProgress<int> progress,
    CancellationToken ct
)
{
    public async Task ProcessAsync()
    {
        // initialize
        var startTime = DateTime.Now;

        // process the job
        try
        {
            await ProcessQueueItemAsync(startTime);
        }

        // when non-retryable errors are encountered
        // we must still remove the queue-item and add
        // it to the history as a failed job.
        catch (Exception e) when (e.IsNonRetryableDownloadException())
        {
            try
            {
                await MarkQueueItemCompleted(startTime, error: e.Message);
            }
            catch (Exception ex)
            {
                Log.Error(e, ex.Message);
            }
        }

        // when an unknown error is encountered
        // let's not remove the item from the queue
        // to give it a chance to retry. Simply
        // log the error and retry in a minute.
        catch (Exception e)
        {
            try
            {
                Log.Error($"Failed to process job, `{queueItem.JobName}` -- {e.Message} -- {e}");
                dbClient.Ctx.ChangeTracker.Clear();
                queueItem.PauseUntil = DateTime.Now.AddMinutes(1);
                dbClient.Ctx.QueueItems.Attach(queueItem);
                dbClient.Ctx.Entry(queueItem).Property(x => x.PauseUntil).IsModified = true;
                await dbClient.Ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
        }
    }

    private async Task ProcessQueueItemAsync(DateTime startTime)
    {
        // This NZB is already processed and mounted
        if (await IsAlreadyDownloaded())
        {
            Log.Information($"Nzb `{queueItem.JobName}` is a duplicate. Skipping and marking complete.");
            await MarkQueueItemCompleted(startTime);
            return;
        }

        // read the nzb document
        var documentBytes = Encoding.UTF8.GetBytes(queueItem.NzbContents);
        using var stream = new MemoryStream(documentBytes);
        var nzb = await NzbDocument.LoadAsync(stream);

        // parse filenames for each nzb file
        var filenamesTaskDictionary = nzb.Files.ToDictionary(x => x, x => x.GetFileName(usenetClient));
        var filenamesDictionary = new Dictionary<NzbFile, string>();
        foreach (var filenameTask in filenamesTaskDictionary)
            filenamesDictionary[filenameTask.Key] = await filenameTask.Value;

        // start file processing tasks
        var fileProcessingTasks = nzb.Files
            .DistinctBy(x => filenamesDictionary[x])
            .Select(x => GetFileProcessor(x, filenamesDictionary[x]))
            .Where(x => x is not null)
            .Select(x => x!.ProcessAsync())
            .ToList();

        // wait for all file processing tasks to finish
        var fileProcessingResults = (await TaskUtil.WhenAllOrError(fileProcessingTasks, progress))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        // update the database
        await MarkQueueItemCompleted(startTime, error: null, () =>
        {
            var categoryFolder = GetOrCreateCategoryFolder();
            var mountFolder = CreateMountFolder(categoryFolder);
            new RarAggregator(dbClient, mountFolder).UpdateDatabase(fileProcessingResults);
            new FileAggregator(dbClient, mountFolder).UpdateDatabase(fileProcessingResults);

            // validate video files found
            if (configManager.IsEnsureImportableVideoEnabled())
                new EnsureImportableVideoValidator(dbClient).ThrowIfValidationFails();
        });
    }

    private BaseProcessor? GetFileProcessor(NzbFile nzbFile, string filename)
    {
        return RarProcessor.CanProcess(filename) ? new RarProcessor(nzbFile, filename, usenetClient, ct)
            : FileProcessor.CanProcess(filename) ? new FileProcessor(nzbFile, filename, usenetClient, ct)
            : null;
    }

    private async Task<bool> IsAlreadyDownloaded()
    {
        var query = from mountFolder in dbClient.Ctx.Items
            join categoryFolder in dbClient.Ctx.Items on mountFolder.ParentId equals categoryFolder.Id
            where mountFolder.Name == queueItem.JobName
                && mountFolder.ParentId != null
                && categoryFolder.Name == queueItem.Category
                && categoryFolder.ParentId == DavItem.ContentFolder.Id
            select mountFolder;

        return await query.AnyAsync();
    }

    private DavItem GetOrCreateCategoryFolder()
    {
        // if the category item already exists, return it
        var categoryFolder = dbClient.Ctx.Items
            .FirstOrDefault(x => x.Parent == DavItem.ContentFolder && x.Name == queueItem.Category);
        if (categoryFolder is not null)
            return categoryFolder;

        // otherwise, create it
        categoryFolder = new DavItem()
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.Now,
            ParentId = DavItem.ContentFolder.Id,
            Name = queueItem.Category,
            Type = DavItem.ItemType.Directory,
        };
        dbClient.Ctx.Items.Add(categoryFolder);
        return categoryFolder;
    }

    private DavItem CreateMountFolder(DavItem categoryFolder)
    {
        var mountFolder = new DavItem()
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.Now,
            ParentId = categoryFolder.Id,
            Name = queueItem.JobName,
            Type = DavItem.ItemType.Directory,
        };
        dbClient.Ctx.Items.Add(mountFolder);
        return mountFolder;
    }

    private HistoryItem CreateHistoryItem(DateTime jobStartTime, string? errorMessage = null)
    {
        return new HistoryItem()
        {
            Id = queueItem.Id,
            CreatedAt = DateTime.Now,
            FileName = queueItem.FileName,
            JobName = queueItem.JobName,
            Category = queueItem.Category,
            DownloadStatus = errorMessage == null
                ? HistoryItem.DownloadStatusOption.Completed
                : HistoryItem.DownloadStatusOption.Failed,
            TotalSegmentBytes = queueItem.TotalSegmentBytes,
            DownloadTimeSeconds = (int)(DateTime.Now - jobStartTime).TotalSeconds,
            FailMessage = errorMessage
        };
    }

    private async Task MarkQueueItemCompleted
    (
        DateTime startTime,
        string? error = null,
        Action? databaseOperations = null
    )
    {
        dbClient.Ctx.ChangeTracker.Clear();
        databaseOperations?.Invoke();
        dbClient.Ctx.QueueItems.Remove(queueItem);
        dbClient.Ctx.HistoryItems.Add(CreateHistoryItem(startTime, error));
        await dbClient.Ctx.SaveChangesAsync(ct);
    }
}
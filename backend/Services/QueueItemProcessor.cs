using System.Text;
using Microsoft.EntityFrameworkCore;
using NzbWebDAV.Clients;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using NzbWebDAV.Extensions;
using NzbWebDAV.Services.FileAggregators;
using NzbWebDAV.Services.FileProcessors;
using NzbWebDAV.Services.Validators;
using NzbWebDAV.Utils;
using Microsoft.Extensions.Logging;
using Usenet.Nzb;

namespace NzbWebDAV.Services;

public class QueueItemProcessor(
    QueueItem queueItem,
    DavDatabaseClient dbClient,
    UsenetProviderManager usenetClient,
    ConfigManager configManager,
    IProgress<int> progress,
    CancellationToken ct,
    ILogger<QueueItemProcessor> logger
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
                logger.LogError(e, "Failed to mark queue item as completed for job '{JobName}': {ErrorMessage}", queueItem.JobName, ex.Message);
            }
        }

        // when retryable errors are encountered (like missing articles)
        // retry up to a limit, then fail permanently
        catch (Exception e) when (e.IsRetryableDownloadException())
        {
            const int maxRetries = 3;
            
            try
            {
                logger.LogWarning("Job '{JobName}' failed, retry {RetryAttempt}/{MaxRetries}: {ErrorMessage}", 
                    queueItem.JobName, queueItem.RetryCount + 1, maxRetries, e.Message);
                dbClient.Ctx.ChangeTracker.Clear();
                
                queueItem.RetryCount++;
                
                // If we've exceeded max retries, fail permanently
                if (queueItem.RetryCount >= maxRetries)
                {
                    logger.LogError("Job '{JobName}' failed permanently after {MaxRetries} retries, moving to history: {FinalErrorMessage}", 
                        queueItem.JobName, maxRetries, e.Message);
                    await MarkQueueItemCompleted(startTime, error: $"Failed after {maxRetries} retries: {e.Message}");
                }
                else
                {
                    // Retry with custom delays: 15s, 30s, 1m
                    var delaySeconds = queueItem.RetryCount switch
                    {
                        1 => 15,  // First retry: 15 seconds
                        2 => 30,  // Second retry: 30 seconds
                        _ => 60   // Third retry: 1 minute
                    };
                    queueItem.PauseUntil = DateTime.Now.AddSeconds(delaySeconds);
                    
                    dbClient.Ctx.QueueItems.Attach(queueItem);
                    dbClient.Ctx.Entry(queueItem).Property(x => x.PauseUntil).IsModified = true;
                    dbClient.Ctx.Entry(queueItem).Property(x => x.RetryCount).IsModified = true;
                    await dbClient.Ctx.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update retry count for job '{JobName}': {ErrorMessage}", queueItem.JobName, ex.Message);
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
                logger.LogError(e, "Unexpected error processing job '{JobName}', will retry in 1 minute: {ErrorMessage}", 
                    queueItem.JobName, e.Message);
                dbClient.Ctx.ChangeTracker.Clear();
                queueItem.PauseUntil = DateTime.Now.AddMinutes(1);
                dbClient.Ctx.QueueItems.Attach(queueItem);
                dbClient.Ctx.Entry(queueItem).Property(x => x.PauseUntil).IsModified = true;
                await dbClient.Ctx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to update pause time for job '{JobName}': {ErrorMessage}", queueItem.JobName, ex.Message);
            }
        }
    }

    private async Task ProcessQueueItemAsync(DateTime startTime)
    {
        // This NZB is already processed and mounted
        if (await IsAlreadyDownloaded())
        {
            logger.LogInformation("Job '{JobName}' already exists, skipping duplicate download", queueItem.JobName);
            await MarkQueueItemCompleted(startTime);
            return;
        }

        // read the nzb document
        var documentBytes = Encoding.UTF8.GetBytes(queueItem.NzbContents);
        using var stream = new MemoryStream(documentBytes);
        var nzb = await NzbDocument.LoadAsync(stream);

        // start the file processing tasks
        var fileProcessingTasks = nzb.Files
            .DistinctBy(x => x.GetSubjectFileName())
            .Select(GetFileProcessor)
            .Where(x => x is not null)
            .Select(x => x!.ProcessAsync())
            .ToList();

        // wait for all file processing tasks to finish
        var fileProcessingResults = await TaskUtil.WhenAllOrError(fileProcessingTasks, progress);

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

    private BaseProcessor? GetFileProcessor(NzbFile nzbFile)
    {
        return RarProcessor.CanProcess(nzbFile) ? new RarProcessor(nzbFile, usenetClient, ct)
            : FileProcessor.CanProcess(nzbFile) ? new FileProcessor(nzbFile, usenetClient, ct)
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
        // Check if mount folder already exists
        var existingMountFolder = dbClient.Ctx.Items
            .FirstOrDefault(x => x.ParentId == categoryFolder.Id && x.Name == queueItem.JobName);
        if (existingMountFolder is not null)
        {
            logger.LogDebug("Mount folder '{JobName}' already exists, reusing it", queueItem.JobName);
            return existingMountFolder;
        }

        // Create new mount folder
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
        try
        {
            dbClient.Ctx.ChangeTracker.Clear();
            databaseOperations?.Invoke();
            dbClient.Ctx.QueueItems.Remove(queueItem);
            dbClient.Ctx.HistoryItems.Add(CreateHistoryItem(startTime, error));
            await dbClient.Ctx.SaveChangesAsync(ct);
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE constraint failed
        {
            logger.LogWarning("Database constraint violation completing job '{JobName}': {ErrorMessage} (job may have been processed already)", 
                queueItem.JobName, ex.Message);
            
            // Clear the context and try to remove just the queue item
            dbClient.Ctx.ChangeTracker.Clear();
            
            // Verify the queue item still exists before removing
            var existingQueueItem = await dbClient.Ctx.QueueItems.FindAsync(queueItem.Id);
            if (existingQueueItem != null)
            {
                dbClient.Ctx.QueueItems.Remove(existingQueueItem);
                dbClient.Ctx.HistoryItems.Add(CreateHistoryItem(startTime, error));
                await dbClient.Ctx.SaveChangesAsync(ct);
            }
        }
    }
}
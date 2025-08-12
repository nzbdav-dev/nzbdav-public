using NzbWebDAV.Clients;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using Serilog;

namespace NzbWebDAV.Queue;

public class QueueManager : IDisposable
{
    private InProgressQueueItem? _inProgressQueueItem;

    private readonly UsenetStreamingClient _usenetClient;
    private readonly CancellationTokenSource? _cancellationTokenSource;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConfigManager _configManager;

    public QueueManager(UsenetStreamingClient usenetClient, ConfigManager configManager)
    {
        _usenetClient = usenetClient;
        _configManager = configManager;
        _cancellationTokenSource = new CancellationTokenSource();
        _ = ProcessQueueAsync(_cancellationTokenSource.Token);
    }

    public (QueueItem? queueItem, int? progress) GetInProgressQueueItem()
    {
        return (_inProgressQueueItem?.QueueItem, _inProgressQueueItem?.ProgressPercentage);
    }

    public async Task RemoveQueueItemAsync(string queueItemId, DavDatabaseClient dbClient)
    {
        await LockAsync(async () =>
        {
            if (_inProgressQueueItem?.QueueItem?.Id.ToString() == queueItemId)
            {
                await _inProgressQueueItem.CancellationTokenSource.CancelAsync();
                await _inProgressQueueItem.ProcessingTask;
            }

            await dbClient.RemoveQueueItemAsync(queueItemId);
        });
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // get the next queue-item from the database
                await using var dbContext = new DavDatabaseContext();
                var dbClient = new DavDatabaseClient(dbContext);
                var queueItem = await dbClient.GetTopQueueItem(ct);
                if (queueItem is null)
                {
                    // if we're done with the queue, wait
                    // five seconds before checking again.
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    continue;
                }

                // process the queue-item
                using var queueItemCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
                await LockAsync(() =>
                {
                    _inProgressQueueItem = BeginProcessingQueueItem(
                        dbClient, queueItem, queueItemCancellationTokenSource
                    );
                });
                await (_inProgressQueueItem?.ProcessingTask ?? Task.CompletedTask);
                await LockAsync(() => { _inProgressQueueItem = null; });
            }
            catch (OperationCanceledException)
            {
                // When the DeleteQueueItemAsync is called and the queue-item is currently
                // being processed, we explicitly cancel the processing of the queue-item.
                // Since this is expected API behavior, we can ignore this exception.
            }
            catch (Exception e)
            {
                Log.Error($"An unexpected error occured while processing the queue: {e.Message}");
            }
        }
    }

    private InProgressQueueItem BeginProcessingQueueItem
    (
        DavDatabaseClient dbClient,
        QueueItem queueItem,
        CancellationTokenSource cts
    )
    {
        var progressHook = new Progress<int>();
        var task = new QueueItemProcessor(
            queueItem, dbClient, _usenetClient, _configManager, progressHook, cts.Token
        ).ProcessAsync();
        var inProgressQueueItem = new InProgressQueueItem()
        {
            QueueItem = queueItem,
            ProcessingTask = task,
            ProgressPercentage = 0,
            CancellationTokenSource = cts
        };
        progressHook.ProgressChanged += (_, progress) =>
            inProgressQueueItem.ProgressPercentage = progress;
        return inProgressQueueItem;
    }

    private async Task LockAsync(Func<Task> actionAsync)
    {
        await _semaphore.WaitAsync();
        try
        {
            await actionAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task LockAsync(Action action)
    {
        await _semaphore.WaitAsync();
        try
        {
            action();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }

    private class InProgressQueueItem
    {
        public QueueItem QueueItem { get; init; }
        public int ProgressPercentage { get; set; }
        public Task ProcessingTask { get; init; }
        public CancellationTokenSource CancellationTokenSource { get; init; }
    }
}
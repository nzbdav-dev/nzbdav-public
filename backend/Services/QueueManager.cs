using NzbWebDAV.Clients;
using NzbWebDAV.Config;
using NzbWebDAV.Database;
using NzbWebDAV.Database.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace NzbWebDAV.Services;

public class QueueManager : IDisposable
{
    private InProgressQueueItem? _inProgressQueueItem;

    private readonly UsenetProviderManager _usenetClient;
    private readonly CancellationTokenSource? _cancellationTokenSource;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConfigManager _configManager;
    private readonly ILogger<QueueManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;

    public QueueManager(UsenetProviderManager usenetClient, ConfigManager configManager, ILogger<QueueManager> logger, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
    {
        _usenetClient = usenetClient;
        _configManager = configManager;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _serviceProvider = serviceProvider;
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
        var emptyQueueCount = 0;
        const int maxEmptyQueueCount = 12; // Max 60 seconds delay
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // get the next queue-item from the database using scoped context
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DavDatabaseContext>();
                var dbClient = new DavDatabaseClient(dbContext);
                var queueItem = await dbClient.GetTopQueueItem(ct);
                if (queueItem is null)
                {
                    // Adaptive polling: increase delay when queue is consistently empty
                    emptyQueueCount = Math.Min(emptyQueueCount + 1, maxEmptyQueueCount);
                    var delaySeconds = Math.Min(5 + emptyQueueCount, 60); // 5-60 second range
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
                    continue;
                }
                
                // Reset empty queue counter when we find work
                emptyQueueCount = 0;

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
                _logger.LogError(e, "Unexpected error occurred while processing the queue: {ErrorMessage}", e.Message);
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
        var processorLogger = _loggerFactory.CreateLogger<QueueItemProcessor>();
        var task = new QueueItemProcessor(
            queueItem, dbClient, _usenetClient, _configManager, progressHook, cts.Token, processorLogger
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
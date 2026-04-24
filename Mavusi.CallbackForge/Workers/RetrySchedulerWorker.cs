using Mavusi.CallbackForge.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mavusi.CallbackForge.Workers;

public sealed class RetrySchedulerWorker : BackgroundService
{
    private readonly IJobStore _jobStore;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<RetrySchedulerWorker> _logger;
    private readonly TimeSpan _scanInterval;

    public RetrySchedulerWorker(
        IJobStore jobStore,
        IJobQueue jobQueue,
        ILogger<RetrySchedulerWorker> logger,
        TimeSpan? scanInterval = null)
    {
        _jobStore = jobStore;
        _jobQueue = jobQueue;
        _logger = logger;
        _scanInterval = scanInterval ?? TimeSpan.FromSeconds(30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RetrySchedulerWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndEnqueueRetries(stoppingToken);
                await Task.Delay(_scanInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in retry scheduler loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("RetrySchedulerWorker stopped");
    }

    private async Task ScanAndEnqueueRetries(CancellationToken cancellationToken)
    {
        var jobsToRetry = await _jobStore.GetJobsReadyForRetryAsync(100, cancellationToken);

        if (jobsToRetry.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} jobs ready for retry", jobsToRetry.Count);

        foreach (var job in jobsToRetry)
        {
            try
            {
                job.NextAttemptAt = null;
                await _jobStore.UpdateAsync(job, cancellationToken);
                await _jobQueue.EnqueueAsync(job.Id, cancellationToken);

                _logger.LogInformation("Enqueued job {JobId} for retry", job.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue job {JobId} for retry", job.Id);
            }
        }
    }
}

using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mavusi.CallbackForge.Workers;

public sealed class CallbackRetryWorker : BackgroundService
{
    private readonly IJobStore _jobStore;
    private readonly ICallbackDispatcher _callbackDispatcher;
    private readonly ILogger<CallbackRetryWorker> _logger;
    private readonly TimeSpan _scanInterval;

    public CallbackRetryWorker(
        IJobStore jobStore,
        ICallbackDispatcher callbackDispatcher,
        ILogger<CallbackRetryWorker> logger,
        TimeSpan? scanInterval = null)
    {
        _jobStore = jobStore;
        _callbackDispatcher = callbackDispatcher;
        _logger = logger;
        _scanInterval = scanInterval ?? TimeSpan.FromSeconds(30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CallbackRetryWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndRetryCallbacks(stoppingToken);
                await Task.Delay(_scanInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in callback retry loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("CallbackRetryWorker stopped");
    }

    private async Task ScanAndRetryCallbacks(CancellationToken cancellationToken)
    {
        var jobsReadyForRetry = await _jobStore.GetJobsReadyForRetryAsync(100, cancellationToken);
        var now = DateTime.UtcNow;

        var callbackJobs = jobsReadyForRetry
            .Where(j => j.Status == JobStatus.Completed)
            .Where(j => j.Callback != null)
            .Where(j => j.Callback!.Status == CallbackStatus.Pending)
            .Where(j => j.Callback!.NextAttemptAt.HasValue && j.Callback.NextAttemptAt.Value <= now)
            .ToList();

        if (callbackJobs.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} callbacks ready for retry", callbackJobs.Count);

        foreach (var job in callbackJobs)
        {
            try
            {
                await _callbackDispatcher.DispatchAsync(job, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retry callback for job {JobId}", job.Id);
            }
        }
    }
}

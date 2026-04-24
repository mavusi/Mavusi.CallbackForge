using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;
using Mavusi.CallbackForge.Pipeline;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mavusi.CallbackForge.Workers;

public sealed class HttpFlowWorker : BackgroundService
{
    private readonly IJobQueue _jobQueue;
    private readonly IJobStore _jobStore;
    private readonly ExecutionPipeline _pipeline;
    private readonly ILogger<HttpFlowWorker> _logger;
    private readonly TimeSpan _lockDuration;
    private readonly TimeSpan _dequeueDelay;

    public HttpFlowWorker(
        IJobQueue jobQueue,
        IJobStore jobStore,
        ExecutionPipeline pipeline,
        ILogger<HttpFlowWorker> logger,
        TimeSpan? lockDuration = null,
        TimeSpan? dequeueDelay = null)
    {
        _jobQueue = jobQueue;
        _jobStore = jobStore;
        _pipeline = pipeline;
        _logger = logger;
        _lockDuration = lockDuration ?? TimeSpan.FromMinutes(5);
        _dequeueDelay = dequeueDelay ?? TimeSpan.FromMilliseconds(100);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HttpFlowWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var jobId = await _jobQueue.DequeueAsync(stoppingToken);

                if (jobId == null)
                {
                    await Task.Delay(_dequeueDelay, stoppingToken);
                    continue;
                }

                await ProcessJobAsync(jobId.Value, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker loop");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        _logger.LogInformation("HttpFlowWorker stopped");
    }

    private async Task ProcessJobAsync(JobId jobId, CancellationToken cancellationToken)
    {
        var lockAcquired = false;

        try
        {
            lockAcquired = await _jobStore.TryAcquireLockAsync(jobId, _lockDuration, cancellationToken);

            if (!lockAcquired)
            {
                _logger.LogWarning("Failed to acquire lock for job {JobId}, re-queueing", jobId);
                await _jobQueue.EnqueueAsync(jobId, cancellationToken);
                return;
            }

            var job = await _jobStore.GetByIdAsync(jobId, cancellationToken);

            if (job == null)
            {
                _logger.LogWarning("Job {JobId} not found", jobId);
                return;
            }

            if (job.Status == JobStatus.Completed || job.Status == JobStatus.Cancelled)
            {
                _logger.LogInformation("Job {JobId} already in terminal state: {Status}", jobId, job.Status);
                return;
            }

            _logger.LogInformation("Processing job {JobId}, attempt {Attempt}", jobId, job.Attempts + 1);

            await _pipeline.ExecuteAsync(job, cancellationToken);

            _logger.LogInformation("Job {JobId} completed with status {Status}", jobId, job.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}", jobId);
        }
        finally
        {
            if (lockAcquired)
            {
                await _jobStore.ReleaseLockAsync(jobId, cancellationToken);
            }
        }
    }
}

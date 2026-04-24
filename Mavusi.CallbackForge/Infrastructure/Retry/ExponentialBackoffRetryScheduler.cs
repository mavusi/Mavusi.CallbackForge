using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Infrastructure.Retry;

public sealed class ExponentialBackoffRetryScheduler : IRetryScheduler
{
    private readonly IJobStore _jobStore;
    private readonly IJobQueue _jobQueue;
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;

    public ExponentialBackoffRetryScheduler(
        IJobStore jobStore,
        IJobQueue jobQueue,
        int maxAttempts = 5,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null)
    {
        _jobStore = jobStore;
        _jobQueue = jobQueue;
        _maxAttempts = maxAttempts;
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(5);
        _maxDelay = maxDelay ?? TimeSpan.FromMinutes(30);
    }

    public async Task ScheduleRetryAsync(Job job, CancellationToken cancellationToken = default)
    {
        if (job.Attempts >= _maxAttempts)
        {
            job.Status = JobStatus.Failed;
            job.FailureReason = $"Max retry attempts ({_maxAttempts}) reached";
            await _jobStore.UpdateAsync(job, cancellationToken);
            return;
        }

        job.NextAttemptAt = CalculateNextAttempt(job.Attempts);
        job.Status = JobStatus.Pending;
        await _jobStore.UpdateAsync(job, cancellationToken);
        await _jobQueue.EnqueueAsync(job.Id, cancellationToken);
    }

    public DateTime CalculateNextAttempt(int attemptNumber)
    {
        var multiplier = Math.Pow(2, attemptNumber);
        var exponentialDelay = TimeSpan.FromSeconds(_baseDelay.TotalSeconds * multiplier);
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
        var delay = TimeSpan.FromTicks(Math.Min(exponentialDelay.Ticks, _maxDelay.Ticks)) + jitter;

        return DateTime.UtcNow.Add(delay);
    }
}

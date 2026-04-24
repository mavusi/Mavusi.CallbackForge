using System.Collections.Concurrent;
using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Infrastructure.Persistence;

public sealed class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<JobId, Job> _jobs = new();
    private readonly ConcurrentDictionary<string, JobId> _idempotencyIndex = new();
    private readonly ConcurrentDictionary<JobId, (DateTime ExpiresAt, SemaphoreSlim Lock)> _locks = new();

    public Task<Job> CreateAsync(Job job, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryAdd(job.Id, job))
        {
            throw new InvalidOperationException($"Job with ID {job.Id} already exists");
        }

        if (!string.IsNullOrEmpty(job.Request.IdempotencyKey))
        {
            _idempotencyIndex.TryAdd(job.Request.IdempotencyKey, job.Id);
        }

        return Task.FromResult(job);
    }

    public Task<Job?> GetByIdAsync(JobId id, CancellationToken cancellationToken = default)
    {
        _jobs.TryGetValue(id, out var job);
        return Task.FromResult(job);
    }

    public Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (_idempotencyIndex.TryGetValue(idempotencyKey, out var jobId))
        {
            _jobs.TryGetValue(jobId, out var job);
            return Task.FromResult(job);
        }

        return Task.FromResult<Job?>(null);
    }

    public Task UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        job.UpdatedAt = DateTime.UtcNow;
        _jobs[job.Id] = job;
        return Task.CompletedTask;
    }

    public async Task<bool> TryAcquireLockAsync(JobId id, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(lockDuration);

        var lockEntry = _locks.GetOrAdd(id, _ => (expiresAt, new SemaphoreSlim(1, 1)));

        if (lockEntry.ExpiresAt < now)
        {
            _locks.TryRemove(id, out _);
            lockEntry = _locks.GetOrAdd(id, _ => (expiresAt, new SemaphoreSlim(1, 1)));
        }

        var acquired = await lockEntry.Lock.WaitAsync(0, cancellationToken);

        if (acquired)
        {
            _locks[id] = (expiresAt, lockEntry.Lock);
        }

        return acquired;
    }

    public Task ReleaseLockAsync(JobId id, CancellationToken cancellationToken = default)
    {
        if (_locks.TryGetValue(id, out var lockEntry))
        {
            lockEntry.Lock.Release();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Job>> GetJobsReadyForRetryAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var jobs = _jobs.Values
            .Where(j => j.NextAttemptAt.HasValue && j.NextAttemptAt.Value <= now)
            .Where(j => j.Status == JobStatus.Failed || j.Status == JobStatus.Pending)
            .OrderBy(j => j.NextAttemptAt)
            .Take(maxCount)
            .ToList();

        return Task.FromResult<IReadOnlyList<Job>>(jobs);
    }
}

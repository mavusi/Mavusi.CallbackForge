using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Abstractions;

public interface IJobStore
{
    Task<Job> CreateAsync(Job job, CancellationToken cancellationToken = default);
    Task<Job?> GetByIdAsync(JobId id, CancellationToken cancellationToken = default);
    Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task UpdateAsync(Job job, CancellationToken cancellationToken = default);
    Task<bool> TryAcquireLockAsync(JobId id, TimeSpan lockDuration, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(JobId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Job>> GetJobsReadyForRetryAsync(int maxCount, CancellationToken cancellationToken = default);
}

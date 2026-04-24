using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Abstractions;

public interface IJobQueue
{
    Task EnqueueAsync(JobId id, CancellationToken cancellationToken = default);
    Task<JobId?> DequeueAsync(CancellationToken cancellationToken = default);
    Task<int> GetQueueLengthAsync(CancellationToken cancellationToken = default);
}

using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Abstractions;

public interface IRetryScheduler
{
    Task ScheduleRetryAsync(Job job, CancellationToken cancellationToken = default);
    DateTime CalculateNextAttempt(int attemptNumber);
}

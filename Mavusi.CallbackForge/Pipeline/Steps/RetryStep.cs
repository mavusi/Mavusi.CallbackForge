using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Pipeline.Steps;

public sealed class RetryStep : IExecutionStep
{
    private readonly IRetryScheduler _retryScheduler;

    public RetryStep(IRetryScheduler retryScheduler)
    {
        _retryScheduler = retryScheduler;
    }

    public async Task ExecuteAsync(JobContext context)
    {
        var job = context.Job;
        var success = context.Metadata.TryGetValue("HttpSuccess", out var httpSuccessObj) 
                      && httpSuccessObj is bool httpSuccess 
                      && httpSuccess;

        if (!success)
        {
            await _retryScheduler.ScheduleRetryAsync(job, context.CancellationToken);
            context.Metadata["RetryScheduled"] = true;
        }
    }
}

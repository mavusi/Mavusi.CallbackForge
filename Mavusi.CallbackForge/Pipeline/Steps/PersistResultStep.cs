using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Pipeline.Steps;

public sealed class PersistResultStep : IExecutionStep
{
    private readonly IJobStore _jobStore;

    public PersistResultStep(IJobStore jobStore)
    {
        _jobStore = jobStore;
    }

    public async Task ExecuteAsync(JobContext context)
    {
        var job = context.Job;

        var success = context.Metadata.TryGetValue("HttpSuccess", out var httpSuccessObj) 
                      && httpSuccessObj is bool httpSuccess 
                      && httpSuccess;

        if (success)
        {
            job.Status = JobStatus.Completed;
            job.FailureReason = null;
        }
        else if (!context.Metadata.ContainsKey("RetryScheduled"))
        {
            job.Status = JobStatus.Failed;
        }

        job.UpdatedAt = DateTime.UtcNow;
        await _jobStore.UpdateAsync(job, context.CancellationToken);
    }
}

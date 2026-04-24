using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Pipeline.Steps;

public sealed class PrepareRequestStep : IExecutionStep
{
    private readonly IJobStore _jobStore;

    public PrepareRequestStep(IJobStore jobStore)
    {
        _jobStore = jobStore;
    }

    public async Task ExecuteAsync(JobContext context)
    {
        var job = context.Job;

        job.Status = JobStatus.Processing;
        job.Attempts++;
        job.UpdatedAt = DateTime.UtcNow;

        await _jobStore.UpdateAsync(job, context.CancellationToken);
    }
}

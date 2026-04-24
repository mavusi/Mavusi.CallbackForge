using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Pipeline.Steps;

public sealed class IdempotencyCheckStep : IExecutionStep
{
    private readonly IJobStore _jobStore;

    public IdempotencyCheckStep(IJobStore jobStore)
    {
        _jobStore = jobStore;
    }

    public async Task ExecuteAsync(JobContext context)
    {
        var job = context.Job;
        var idempotencyKey = job.Request.IdempotencyKey;

        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return;
        }

        var existingJob = await _jobStore.GetByIdempotencyKeyAsync(idempotencyKey, context.CancellationToken);

        if (existingJob != null && existingJob.Id != job.Id)
        {
            if (existingJob.Status == JobStatus.Completed)
            {
                context.Metadata["IdempotencyMatch"] = true;
                context.Metadata["ExistingJob"] = existingJob;

                job.Status = JobStatus.Completed;
                job.Response = existingJob.Response;
                await _jobStore.UpdateAsync(job, context.CancellationToken);
            }
        }
    }
}

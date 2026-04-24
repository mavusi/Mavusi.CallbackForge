using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Client;

public sealed class HttpFlowClient
{
    private readonly IJobStore _jobStore;
    private readonly IJobQueue _jobQueue;

    public HttpFlowClient(IJobStore jobStore, IJobQueue jobQueue)
    {
        _jobStore = jobStore;
        _jobQueue = jobQueue;
    }

    public async Task<JobId> SubmitAsync(
        HttpRequest request,
        CallbackInfo? callback = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existingJob = await _jobStore.GetByIdempotencyKeyAsync(
                request.IdempotencyKey,
                cancellationToken
            );

            if (existingJob != null)
            {
                return existingJob.Id;
            }
        }

        var job = new Job
        {
            Id = JobId.New(),
            Status = JobStatus.Pending,
            Request = request,
            Callback = callback,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _jobStore.CreateAsync(job, cancellationToken);
        await _jobQueue.EnqueueAsync(job.Id, cancellationToken);

        return job.Id;
    }

    public async Task<Job?> GetJobAsync(JobId jobId, CancellationToken cancellationToken = default)
    {
        return await _jobStore.GetByIdAsync(jobId, cancellationToken);
    }

    private static void ValidateRequest(HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            throw new ArgumentException("URL is required", nameof(request));
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Invalid URL format", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Method))
        {
            throw new ArgumentException("HTTP method is required", nameof(request));
        }

        var validMethods = new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };
        if (!validMethods.Contains(request.Method.ToUpperInvariant()))
        {
            throw new ArgumentException($"Invalid HTTP method: {request.Method}", nameof(request));
        }
    }
}

# Troubleshooting & FAQ

## Common Issues

### Jobs Not Processing

**Symptoms**: Jobs stay in `Pending` status indefinitely

**Possible Causes**:
1. Workers disabled in configuration
2. Worker crashed or not started
3. Queue implementation issue

**Solutions**:
```csharp
// Check if workers are enabled
builder.Services.AddCallbackForge(options =>
{
    options.EnableWorkers = true; // Make sure this is true
});

// Check worker logs
// Look for "HttpFlowWorker started" in logs

// Manually check queue length
var queue = services.GetRequiredService<IJobQueue>();
var length = await queue.GetQueueLengthAsync();
Console.WriteLine($"Queue length: {length}");
```

---

### Callbacks Not Being Delivered

**Symptoms**: Jobs complete but callbacks never arrive

**Possible Causes**:
1. Invalid callback URL
2. Callback endpoint returning errors
3. Network/firewall issues
4. Max callback attempts reached

**Solutions**:
```csharp
// Check callback status
var job = await client.GetJobAsync(jobId);
Console.WriteLine($"Callback Status: {job.Callback?.Status}");
Console.WriteLine($"Callback Attempts: {job.Callback?.Attempts}");
Console.WriteLine($"Callback Failure: {job.Callback?.FailureReason}");

// Test callback URL manually
var httpClient = new HttpClient();
var response = await httpClient.PostAsync(callbackUrl, new StringContent("test"));
Console.WriteLine($"Test response: {response.StatusCode}");

// Increase callback attempts
builder.Services.AddCallbackForge(options =>
{
    options.MaxCallbackAttempts = 5; // Increase from default 3
    options.CallbackTimeout = TimeSpan.FromSeconds(60); // Increase timeout
});
```

---

### Jobs Stuck in Processing

**Symptoms**: Jobs show `Processing` status but never complete

**Possible Causes**:
1. Worker crashed while processing
2. Lock not released
3. HTTP request hanging

**Solutions**:
```csharp
// Check lock duration
builder.Services.AddCallbackForge(options =>
{
    options.WorkerLockDuration = TimeSpan.FromMinutes(2); // Reduce if too long
});

// Locks expire automatically after duration
// Job will be retried by another worker

// Add request timeouts
var request = new HttpRequest
{
    Url = "https://api.example.com",
    Method = "GET",
    Timeout = TimeSpan.FromSeconds(30) // Explicit timeout
};
```

---

### High Memory Usage

**Symptoms**: Memory usage grows over time

**Possible Causes**:
1. In-memory store accumulating jobs
2. Queue growing unbounded
3. Response bodies too large

**Solutions**:
```csharp
// In production, use persistent storage
builder.Services.AddSingleton<IJobStore, SqlJobStore>(); // Not in-memory

// Implement job cleanup
public class JobCleanupWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Delete completed jobs older than 7 days
            await DeleteOldJobs();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}

// Limit response body size
// Consider storing large responses externally (S3, Blob Storage)
```

---

### Duplicate Job Execution

**Symptoms**: Same job processed multiple times

**Possible Causes**:
1. Idempotency key not used
2. Lock implementation issue
3. Multiple workers, clock skew

**Solutions**:
```csharp
// Always use idempotency keys for important operations
var request = new HttpRequest
{
    Url = "https://api.example.com/payment",
    Method = "POST",
    IdempotencyKey = $"{userId}-{orderId}-{timestamp}" // Unique key
};

// Ensure proper lock implementation
// Use Redis locks for distributed systems
builder.Services.AddSingleton<IJobStore, RedisJobStore>();

// Check for multiple executions
var job = await client.GetJobAsync(jobId);
Console.WriteLine($"Attempts: {job.Attempts}"); // Should be 1 for successful job
```

---

### Slow Job Processing

**Symptoms**: Jobs take longer than expected

**Possible Causes**:
1. Single worker processing sequentially
2. Network latency
3. Target API slow
4. Too many pipeline steps

**Solutions**:
```csharp
// Scale horizontally - run multiple instances
// Each instance runs workers independently

// Monitor queue length
var queue = services.GetRequiredService<IJobQueue>();
var length = await queue.GetQueueLengthAsync();
if (length > 100)
{
    // Consider adding more workers
}

// Reduce dequeue delay for faster processing
builder.Services.AddCallbackForge(options =>
{
    options.WorkerDequeueDelay = TimeSpan.FromMilliseconds(10); // Faster polling
});

// Optimize HTTP execution
var request = new HttpRequest
{
    Timeout = TimeSpan.FromSeconds(5) // Shorter timeout
};
```

---

## Frequently Asked Questions

### Q: Can I run multiple worker instances?

**A**: Yes! CallbackForge is designed for horizontal scaling. Use a shared queue (Redis) and storage (SQL/MongoDB) with distributed locking.

```csharp
// Service 1, 2, 3... all sharing Redis and SQL
builder.Services.AddSingleton<IJobQueue, RedisJobQueue>();
builder.Services.AddSingleton<IJobStore, SqlJobStore>();
```

---

### Q: How do I handle authentication in HTTP requests?

**A**: Add authentication headers to the request:

```csharp
var request = new HttpRequest
{
    Url = "https://api.example.com/data",
    Method = "GET",
    Headers = new Dictionary<string, string>
    {
        { "Authorization", $"Bearer {accessToken}" },
        { "X-API-Key", apiKey }
    }
};
```

---

### Q: Can I retry only specific HTTP status codes?

**A**: Yes, create a custom retry step:

```csharp
public class SelectiveRetryStep : IExecutionStep
{
    private readonly IRetryScheduler _retryScheduler;
    private readonly HashSet<int> _retryableStatusCodes = new() { 408, 429, 500, 502, 503, 504 };

    public async Task ExecuteAsync(JobContext context)
    {
        var success = context.Metadata.TryGetValue("HttpSuccess", out var httpSuccessObj) 
                      && httpSuccessObj is bool httpSuccess 
                      && httpSuccess;

        if (!success)
        {
            var statusCode = context.Job.Response?.StatusCode ?? 0;
            if (_retryableStatusCodes.Contains(statusCode) || statusCode == 0)
            {
                await _retryScheduler.ScheduleRetryAsync(context.Job, context.CancellationToken);
                context.Metadata["RetryScheduled"] = true;
            }
        }
    }
}
```

---

### Q: How do I test without background workers?

**A**: Disable workers and process jobs manually:

```csharp
// In test configuration
builder.Services.AddCallbackForge(options =>
{
    options.EnableWorkers = false;
});

// Manually process job in test
var jobId = await client.SubmitAsync(request);

var jobQueue = services.GetRequiredService<IJobQueue>();
var jobStore = services.GetRequiredService<IJobStore>();
var pipeline = services.GetRequiredService<ExecutionPipeline>();

var dequeuedJobId = await jobQueue.DequeueAsync();
var job = await jobStore.GetByIdAsync(dequeuedJobId!.Value);
await pipeline.ExecuteAsync(job);

// Assert on result
Assert.AreEqual(JobStatus.Completed, job.Status);
```

---

### Q: Can I cancel a job?

**A**: Yes, update the job status:

```csharp
var job = await jobStore.GetByIdAsync(jobId);
if (job != null && job.Status == JobStatus.Pending)
{
    job.Status = JobStatus.Cancelled;
    await jobStore.UpdateAsync(job);
}
```

---

### Q: How do I implement a dead letter queue?

**A**: Create a custom retry scheduler:

```csharp
public class DeadLetterRetryScheduler : IRetryScheduler
{
    private readonly IJobStore _jobStore;
    private readonly IJobQueue _jobQueue;
    private readonly IJobQueue _deadLetterQueue;
    private readonly int _maxAttempts;

    public async Task ScheduleRetryAsync(Job job, CancellationToken cancellationToken = default)
    {
        if (job.Attempts >= _maxAttempts)
        {
            job.Status = JobStatus.Failed;
            await _jobStore.UpdateAsync(job, cancellationToken);

            // Move to dead letter queue
            await _deadLetterQueue.EnqueueAsync(job.Id, cancellationToken);
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
        return DateTime.UtcNow.Add(TimeSpan.FromSeconds(5 * multiplier));
    }
}
```

---

### Q: How do I handle file uploads?

**A**: Convert files to base64 or use multipart form data:

```csharp
// Base64 approach
var fileBytes = await File.ReadAllBytesAsync(filePath);
var base64 = Convert.ToBase64String(fileBytes);

var request = new HttpRequest
{
    Url = "https://api.example.com/upload",
    Method = "POST",
    Headers = new Dictionary<string, string>
    {
        { "Content-Type", "application/json" }
    },
    Body = JsonSerializer.Serialize(new
    {
        filename = "document.pdf",
        content = base64
    })
};

// For large files, consider uploading first then submit job with URL
```

---

### Q: Can I schedule jobs for future execution?

**A**: Yes, set `NextAttemptAt`:

```csharp
var job = new Job
{
    Id = JobId.New(),
    Status = JobStatus.Pending,
    Request = request,
    NextAttemptAt = DateTime.UtcNow.AddHours(2), // Schedule for 2 hours from now
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};

await jobStore.CreateAsync(job);
// Don't enqueue immediately - RetrySchedulerWorker will pick it up
```

---

### Q: How do I implement rate limiting?

**A**: Create a rate-limiting step:

```csharp
public class RateLimitingStep : IExecutionStep
{
    private readonly SemaphoreSlim _semaphore;

    public RateLimitingStep(int maxConcurrent = 10)
    {
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    public async Task ExecuteAsync(JobContext context)
    {
        await _semaphore.WaitAsync(context.CancellationToken);
        try
        {
            // Continue with next steps
            context.Metadata["RateLimitAcquired"] = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

---

### Q: How do I monitor system health?

**A**: Implement health checks:

```csharp
public class CallbackForgeHealthCheck : IHealthCheck
{
    private readonly IJobQueue _queue;
    private readonly IJobStore _store;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queueLength = await _queue.GetQueueLengthAsync(cancellationToken);

            if (queueLength > 1000)
            {
                return HealthCheckResult.Degraded($"Queue length: {queueLength}");
            }

            return HealthCheckResult.Healthy($"Queue length: {queueLength}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to check queue", ex);
        }
    }
}

// Register
builder.Services.AddHealthChecks()
    .AddCheck<CallbackForgeHealthCheck>("callbackforge");
```

---

### Q: Can I use this with Azure Functions?

**A**: Yes, but disable the built-in workers and trigger via queue:

```csharp
// In Startup
builder.Services.AddCallbackForge(options =>
{
    options.EnableWorkers = false; // Functions handle execution
});

// In Azure Function
[FunctionName("ProcessJob")]
public async Task Run(
    [QueueTrigger("callbackforge-queue")] string jobIdString,
    [Inject] ExecutionPipeline pipeline,
    [Inject] IJobStore jobStore)
{
    var jobId = new JobId(Guid.Parse(jobIdString));
    var job = await jobStore.GetByIdAsync(jobId);

    if (job != null)
    {
        await pipeline.ExecuteAsync(job);
    }
}
```

---

## Performance Tuning

### Optimize for Throughput

```csharp
builder.Services.AddCallbackForge(options =>
{
    options.WorkerDequeueDelay = TimeSpan.FromMilliseconds(1); // Fast polling
    options.RetryScanInterval = TimeSpan.FromSeconds(10); // Frequent retry checks
});

// Run multiple worker instances
// Use Redis for queue (faster than SQL)
// Use connection pooling for HTTP
```

### Optimize for Low Resource Usage

```csharp
builder.Services.AddCallbackForge(options =>
{
    options.WorkerDequeueDelay = TimeSpan.FromSeconds(1); // Slow polling
    options.RetryScanInterval = TimeSpan.FromMinutes(1); // Infrequent checks
});

// Single worker instance
// Limit concurrent HTTP connections
// Clean up old jobs regularly
```

---

## Debugging Tips

### Enable Detailed Logging

```csharp
builder.Logging.AddFilter("Mavusi.CallbackForge", LogLevel.Debug);
```

### Inspect Job State

```csharp
var job = await jobStore.GetByIdAsync(jobId);
Console.WriteLine($"Status: {job.Status}");
Console.WriteLine($"Attempts: {job.Attempts}");
Console.WriteLine($"Next Attempt: {job.NextAttemptAt}");
Console.WriteLine($"Failure Reason: {job.FailureReason}");
Console.WriteLine($"Response: {job.Response?.Body}");
```

### Test Components Independently

```csharp
// Test queue
var queue = new InMemoryJobQueue();
await queue.EnqueueAsync(JobId.New());
var jobId = await queue.DequeueAsync();
Assert.IsNotNull(jobId);

// Test store
var store = new InMemoryJobStore();
var job = new Job { /* ... */ };
await store.CreateAsync(job);
var retrieved = await store.GetByIdAsync(job.Id);
Assert.AreEqual(job.Id, retrieved.Id);

// Test pipeline
var pipeline = new ExecutionPipeline(new[] { /* steps */ });
await pipeline.ExecuteAsync(job);
Assert.AreEqual(JobStatus.Completed, job.Status);
```

---

## Getting Help

1. Check logs for error messages
2. Verify configuration settings
3. Test components independently
4. Review architecture documentation
5. Check GitHub issues (if open source)
6. Ask on Stack Overflow with tag `callbackforge`

---

**Remember**: Most issues are related to configuration or custom implementations. Start with the default setup and gradually customize.

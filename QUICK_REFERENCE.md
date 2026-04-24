# CallbackForge Quick Reference

## Installation

```bash
dotnet add package Mavusi.CallbackForge
```

## Configuration

```csharp
// Startup.cs or Program.cs
builder.Services.AddCallbackForge(options =>
{
    options.MaxRetryAttempts = 5;
    options.BaseRetryDelay = TimeSpan.FromSeconds(5);
    options.DefaultRequestTimeout = TimeSpan.FromSeconds(30);
    options.MaxCallbackAttempts = 3;
    options.EnableWorkers = true;
});
```

## Submit a Job

```csharp
// Inject HttpFlowClient
private readonly HttpFlowClient _client;

// Simple GET
var request = new HttpRequest
{
    Url = "https://api.example.com/data",
    Method = "GET"
};

var jobId = await _client.SubmitAsync(request);
```

## With Body & Headers

```csharp
var request = new HttpRequest
{
    Url = "https://api.example.com/data",
    Method = "POST",
    Headers = new Dictionary<string, string>
    {
        { "Authorization", "Bearer token" },
        { "Content-Type", "application/json" }
    },
    Body = """{"key": "value"}""",
    Timeout = TimeSpan.FromSeconds(30)
};
```

## With Callback

```csharp
var callback = new CallbackInfo
{
    Url = "https://myapp.com/webhook",
    Headers = new Dictionary<string, string>
    {
        { "X-Webhook-Secret", "secret" }
    }
};

var jobId = await _client.SubmitAsync(request, callback);
```

## With Idempotency

```csharp
var request = new HttpRequest
{
    Url = "https://api.example.com/payment",
    Method = "POST",
    Body = """{"amount": 100}""",
    IdempotencyKey = "payment-123" // Same key = same job
};
```

## Check Job Status

```csharp
var job = await _client.GetJobAsync(jobId);

Console.WriteLine($"Status: {job.Status}");
Console.WriteLine($"Attempts: {job.Attempts}");

if (job.Response != null)
{
    Console.WriteLine($"Status Code: {job.Response.StatusCode}");
    Console.WriteLine($"Body: {job.Response.Body}");
}
```

## Job Statuses

| Status | Description |
|--------|-------------|
| `Pending` | Waiting for processing |
| `Processing` | Currently executing |
| `Completed` | Successfully finished |
| `Failed` | Failed after all retries |
| `Cancelled` | Manually cancelled |

## Callback Payload

Your webhook receives:

```json
{
  "jobId": "guid",
  "status": "Completed",
  "request": {
    "url": "...",
    "method": "GET",
    "headers": {},
    "body": null
  },
  "response": {
    "statusCode": 200,
    "headers": {},
    "body": "...",
    "duration": 245.5,
    "receivedAt": "2024-01-15T10:30:00Z"
  },
  "attempts": 1,
  "createdAt": "2024-01-15T10:29:59Z",
  "completedAt": "2024-01-15T10:30:01Z"
}
```

## Custom Implementation

### Replace Queue

```csharp
public class RedisJobQueue : IJobQueue
{
    public async Task EnqueueAsync(JobId id, CancellationToken ct = default)
    {
        // Redis implementation
    }

    public async Task<JobId?> DequeueAsync(CancellationToken ct = default)
    {
        // Redis implementation
    }

    public async Task<int> GetQueueLengthAsync(CancellationToken ct = default)
    {
        // Redis implementation
    }
}

// Register
builder.Services.Replace(
    ServiceDescriptor.Singleton<IJobQueue, RedisJobQueue>()
);
```

### Custom Pipeline Step

```csharp
public class MyCustomStep : IExecutionStep
{
    public async Task ExecuteAsync(JobContext context)
    {
        // Your logic here
        var job = context.Job;

        // Access metadata
        context.Metadata["MyKey"] = "MyValue";

        // Modify job
        job.UpdatedAt = DateTime.UtcNow;
    }
}

// Add to pipeline
services.AddTransient<MyCustomStep>();
services.AddSingleton<ExecutionPipeline>(sp => 
    new ExecutionPipeline(new IExecutionStep[]
    {
        sp.GetRequiredService<PrepareRequestStep>(),
        sp.GetRequiredService<IdempotencyCheckStep>(),
        sp.GetRequiredService<MyCustomStep>(), // Your step
        sp.GetRequiredService<HttpExecutionStep>(),
        sp.GetRequiredService<RetryStep>(),
        sp.GetRequiredService<PersistResultStep>(),
        sp.GetRequiredService<CallbackDispatchStep>()
    })
);
```

## Testing Without Workers

```csharp
builder.Services.AddCallbackForge(options =>
{
    options.EnableWorkers = false;
});

// Manual processing
var jobId = await _client.SubmitAsync(request);
var queuedJobId = await _queue.DequeueAsync();
var job = await _store.GetByIdAsync(queuedJobId.Value);
await _pipeline.ExecuteAsync(job);
```

## Common Patterns

### Fire and Forget
```csharp
await _client.SubmitAsync(request);
return Accepted(); // Don't wait for result
```

### Wait for Completion
```csharp
var jobId = await _client.SubmitAsync(request);

while (true)
{
    var job = await _client.GetJobAsync(jobId);
    if (job.Status == JobStatus.Completed)
        return Ok(job.Response);

    await Task.Delay(1000);
}
```

### Webhook Pattern
```csharp
var callback = new CallbackInfo
{
    Url = $"https://myapp.com/webhook/{correlationId}"
};

await _client.SubmitAsync(request, callback);
return Accepted(new { correlationId });
```

### Idempotent Operations
```csharp
var key = $"{resource}-{operation}-{userId}";
var request = new HttpRequest
{
    // ...
    IdempotencyKey = key
};

var jobId = await _client.SubmitAsync(request);
// Duplicate submissions return same jobId
```

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `EnableWorkers` | `true` | Enable background processing |
| `MaxRetryAttempts` | `5` | Max HTTP request retries |
| `BaseRetryDelay` | `5s` | Base delay for backoff |
| `MaxRetryDelay` | `30m` | Maximum retry delay |
| `DefaultRequestTimeout` | `30s` | HTTP request timeout |
| `MaxCallbackAttempts` | `3` | Max callback retries |
| `CallbackTimeout` | `30s` | Callback timeout |
| `WorkerLockDuration` | `5m` | Job lock duration |
| `WorkerDequeueDelay` | `100ms` | Queue polling interval |
| `RetryScanInterval` | `30s` | Retry scan frequency |
| `CallbackScanInterval` | `30s` | Callback retry frequency |

## Logging

```csharp
// Enable detailed logging
builder.Logging.AddFilter("Mavusi.CallbackForge", LogLevel.Debug);

// Logs include:
// - Job submission
// - Processing start/end
// - HTTP request/response
// - Retry attempts
// - Callback delivery
// - Errors and warnings
```

## Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<CallbackForgeHealthCheck>("callbackforge");

// Check queue length, processing rate, etc.
```

## Metrics to Monitor

- Queue length
- Processing time (P50, P95, P99)
- Success rate
- Retry rate
- Callback success rate
- Worker health

## Common Issues

| Issue | Solution |
|-------|----------|
| Jobs not processing | Check `EnableWorkers = true` |
| Callbacks failing | Verify callback URL is accessible |
| High memory usage | Use persistent storage (Redis/SQL) |
| Duplicate execution | Use idempotency keys |
| Slow processing | Scale horizontally, reduce delays |

## REST API Example

```http
### Submit Job
POST http://localhost:5000/api/jobs
Content-Type: application/json

{
  "url": "https://api.example.com/data",
  "method": "GET",
  "callbackUrl": "https://myapp.com/webhook"
}

### Get Job Status
GET http://localhost:5000/api/jobs/{jobId}
```

## Architecture Layers

1. **Ingestion** → HttpFlowClient, IJobStore
2. **Queue** → IJobQueue
3. **Worker** → HttpFlowWorker
4. **Pipeline** → ExecutionPipeline + Steps
5. **Retry** → IRetryScheduler
6. **Callback** → ICallbackDispatcher

## Key Interfaces

- `IJobStore` - Persistence & locking
- `IJobQueue` - Queue operations
- `IExecutionStep` - Pipeline steps
- `IRetryScheduler` - Retry logic
- `ICallbackDispatcher` - Callback delivery

## Dependencies

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
```

## Learn More

- `README.md` - Getting started
- `ARCHITECTURE.md` - Deep dive
- `TROUBLESHOOTING.md` - Problem solving
- `API_EXAMPLES.md` - Request examples
- `CHECKLIST.md` - Implementation guide

---

**Version**: 1.0.0 | **Target**: .NET 8 | **License**: MIT

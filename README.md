# Mavusi.CallbackForge

A production-grade asynchronous API handler for .NET 8 that submits HTTP requests to third-party services, returns immediate "accepted" responses, executes requests in the background, and delivers results to callback endpoints.

## Features

✅ **Production-Ready Architecture** - Layered design with clear separation of concerns  
✅ **Asynchronous Processing** - Non-blocking job submission with background execution  
✅ **Reliable Retries** - Exponential backoff with jitter for failed requests  
✅ **Callback Delivery** - Independent retry mechanism for callback endpoints  
✅ **Idempotency** - Prevent duplicate job execution with idempotency keys  
✅ **Distributed Safety** - Job locking to prevent duplicate processing  
✅ **Resilient** - Timeout handling, partial failure detection, and cancellation support  
✅ **Extensible** - Interface-based design for custom implementations  

## Architecture

### Layers

1. **Ingestion Layer** - Accept and persist jobs safely (`HttpFlowClient`, `IJobStore`)
2. **Queue Layer** - Decouple submission from execution (`IJobQueue`)
3. **Worker Layer** - Background processing engine (`HttpFlowWorker`)
4. **Execution Pipeline** - Composable steps for request processing
5. **Retry Engine** - Smart retry scheduling with exponential backoff
6. **Callback Dispatcher** - Independent callback delivery system
7. **Persistence** - Job state management and locking

### Execution Pipeline Steps

1. `PrepareRequestStep` - Initialize job for processing
2. `IdempotencyCheckStep` - Check for duplicate requests
3. `HttpExecutionStep` - Execute HTTP request
4. `RetryStep` - Schedule retry if needed
5. `PersistResultStep` - Save execution results
6. `CallbackDispatchStep` - Trigger callback delivery

## Installation

```bash
dotnet add package Mavusi.CallbackForge
```

## Quick Start

### 1. Configure Services

```csharp
using Mavusi.CallbackForge.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCallbackForge(options =>
{
    options.MaxRetryAttempts = 5;
    options.BaseRetryDelay = TimeSpan.FromSeconds(5);
    options.DefaultRequestTimeout = TimeSpan.FromSeconds(30);
    options.MaxCallbackAttempts = 3;
});

var app = builder.Build();
app.Run();
```

### 2. Submit a Job

```csharp
using Mavusi.CallbackForge.Client;
using Mavusi.CallbackForge.Models;

public class MyService
{
    private readonly HttpFlowClient _client;

    public MyService(HttpFlowClient client)
    {
        _client = client;
    }

    public async Task<JobId> SubmitOrderProcessing()
    {
        var request = new HttpRequest
        {
            Url = "https://api.example.com/orders",
            Method = "POST",
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer token123" },
                { "Content-Type", "application/json" }
            },
            Body = """{"orderId": "12345", "amount": 99.99}""",
            Timeout = TimeSpan.FromSeconds(30),
            IdempotencyKey = "order-12345" // Optional
        };

        var callback = new CallbackInfo
        {
            Url = "https://myapp.com/webhooks/order-completed",
            Headers = new Dictionary<string, string>
            {
                { "X-Webhook-Secret", "secret123" }
            }
        };

        var jobId = await _client.SubmitAsync(request, callback);
        return jobId; // Return immediately
    }

    public async Task<Job?> CheckJobStatus(JobId jobId)
    {
        return await _client.GetJobAsync(jobId);
    }
}
```

### 3. Handle Callbacks

```csharp
[ApiController]
[Route("webhooks")]
public class WebhookController : ControllerBase
{
    [HttpPost("order-completed")]
    public IActionResult OrderCompleted([FromBody] CallbackPayload payload)
    {
        // Process the callback
        Console.WriteLine($"Job {payload.JobId} completed with status {payload.Status}");

        if (payload.Response != null)
        {
            Console.WriteLine($"Response: {payload.Response.StatusCode} - {payload.Response.Body}");
        }

        return Ok();
    }
}

public class CallbackPayload
{
    public string JobId { get; set; }
    public string Status { get; set; }
    public RequestInfo Request { get; set; }
    public ResponseInfo? Response { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class RequestInfo
{
    public string Url { get; set; }
    public string Method { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string Body { get; set; }
}

public class ResponseInfo
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string Body { get; set; }
    public double Duration { get; set; }
    public DateTime ReceivedAt { get; set; }
}
```

## Configuration Options

```csharp
public class CallbackForgeOptions
{
    // Enable/disable background workers (useful for testing)
    public bool EnableWorkers { get; set; } = true;

    // Maximum retry attempts for HTTP requests
    public int MaxRetryAttempts { get; set; } = 5;

    // Base delay for exponential backoff (doubled each attempt)
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    // Maximum retry delay cap
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(30);

    // Default timeout for HTTP requests
    public TimeSpan DefaultRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    // Maximum callback delivery attempts
    public int MaxCallbackAttempts { get; set; } = 3;

    // Timeout for callback delivery
    public TimeSpan CallbackTimeout { get; set; } = TimeSpan.FromSeconds(30);

    // Duration to hold job lock during processing
    public TimeSpan WorkerLockDuration { get; set; } = TimeSpan.FromMinutes(5);

    // Delay between queue polling when empty
    public TimeSpan WorkerDequeueDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    // How often to scan for jobs ready for retry
    public TimeSpan RetryScanInterval { get; set; } = TimeSpan.FromSeconds(30);

    // How often to scan for callbacks ready for retry
    public TimeSpan CallbackScanInterval { get; set; } = TimeSpan.FromSeconds(30);
}
```

## Advanced Usage

### Custom Implementations

Replace in-memory implementations with production-ready alternatives:

```csharp
// Use Redis for queue
builder.Services.AddSingleton<IJobQueue, RedisJobQueue>();

// Use SQL for persistence
builder.Services.AddSingleton<IJobStore, SqlJobStore>();

// Custom retry strategy
builder.Services.AddSingleton<IRetryScheduler, CustomRetryScheduler>();
```

### Job Status States

- `Pending` - Job submitted, waiting for processing
- `Processing` - Job currently being executed
- `Completed` - Job successfully completed
- `Failed` - Job failed after all retry attempts
- `Cancelled` - Job was cancelled

### Callback Status States

- `Pending` - Callback waiting to be sent
- `InProgress` - Callback currently being sent
- `Completed` - Callback successfully delivered
- `Failed` - Callback failed after all retry attempts

## Implementation Patterns

### Idempotency

```csharp
var request = new HttpRequest
{
    Url = "https://api.example.com/payment",
    Method = "POST",
    Body = """{"amount": 100}""",
    IdempotencyKey = $"payment-{userId}-{orderId}" // Ensures no duplicate charges
};
```

### Retry Behavior

The system uses exponential backoff with jitter:
- Attempt 1: immediate
- Attempt 2: ~5 seconds
- Attempt 3: ~10 seconds
- Attempt 4: ~20 seconds
- Attempt 5: ~40 seconds

### Distributed Safety

Jobs are locked during processing to prevent duplicate execution in multi-instance deployments. Lock expiration ensures stuck jobs are eventually retried.

## Background Workers

Three background workers run continuously:

1. **HttpFlowWorker** - Processes jobs from the queue
2. **RetrySchedulerWorker** - Scans for jobs ready for retry
3. **CallbackRetryWorker** - Retries failed callback deliveries

## Testing

Disable workers for unit testing:

```csharp
builder.Services.AddCallbackForge(options =>
{
    options.EnableWorkers = false;
});

// Manually process jobs in tests
var jobQueue = services.GetRequiredService<IJobQueue>();
var jobStore = services.GetRequiredService<IJobStore>();
var pipeline = services.GetRequiredService<ExecutionPipeline>();

var jobId = await jobQueue.DequeueAsync();
var job = await jobStore.GetByIdAsync(jobId.Value);
await pipeline.ExecuteAsync(job);
```

## Production Considerations

### 1. Replace In-Memory Implementations

The default in-memory implementations are suitable for development but should be replaced for production:

- **Queue**: Use Redis (`StackExchange.Redis`) or Azure Service Bus
- **Storage**: Use SQL Server, PostgreSQL, or MongoDB
- **Locking**: Use Redis distributed locks or database row locks

### 2. Monitoring & Observability

Add logging, metrics, and tracing:

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddApplicationInsights();
});
```

### 3. Scaling

- Run multiple worker instances for horizontal scaling
- Use distributed locks to prevent duplicate processing
- Monitor queue length and adjust worker count

### 4. Error Handling

- Dead letter queue for permanently failed jobs
- Alerting on callback failures
- Circuit breakers for downstream service protection

## License

MIT

## Contributing

Contributions welcome! Please open an issue or submit a PR.

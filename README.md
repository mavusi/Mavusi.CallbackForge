# Mavusi.CallbackForge

A production-grade asynchronous API handler for .NET 8, 9, and 10 that submits HTTP requests to third-party services, returns immediate "accepted" responses, executes requests in the background, and delivers results to callback endpoints.

## Features

✅ **Multi-Target Support** - Compatible with .NET 8, 9, and 10  
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

The default in-memory implementations are suitable for development but should be replaced for production. Below are complete implementation examples for production-grade queues and stores.

---

#### Redis Job Queue

Use Redis for distributed, persistent job queuing across multiple worker instances.

**NuGet Package:**
```bash
dotnet add package StackExchange.Redis
```

**Implementation:**
```csharp
using StackExchange.Redis;
using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

public class RedisJobQueue : IJobQueue
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _queueKey;

    public RedisJobQueue(IConnectionMultiplexer redis, string queueKey = "callbackforge:queue")
    {
        _redis = redis;
        _queueKey = queueKey;
    }

    public async Task EnqueueAsync(JobId id, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        await db.ListRightPushAsync(_queueKey, id.Value.ToString());
    }

    public async Task<JobId?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.ListLeftPopAsync(_queueKey);

        if (value.IsNullOrEmpty)
            return null;

        return new JobId(Guid.Parse(value!));
    }

    public async Task<int> GetQueueLengthAsync(CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        return (int)await db.ListLengthAsync(_queueKey);
    }
}
```

**Configuration:**
```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse("localhost:6379");
    configuration.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddSingleton<IJobQueue, RedisJobQueue>();
```

---

#### Azure Service Bus Job Queue

Use Azure Service Bus for enterprise-grade message queuing with dead-letter support.

**NuGet Package:**
```bash
dotnet add package Azure.Messaging.ServiceBus
```

**Implementation:**
```csharp
using Azure.Messaging.ServiceBus;
using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

public class ServiceBusJobQueue : IJobQueue
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    private readonly ServiceBusReceiver _receiver;
    private readonly string _queueName;

    public ServiceBusJobQueue(string connectionString, string queueName = "callbackforge-jobs")
    {
        _queueName = queueName;
        _client = new ServiceBusClient(connectionString);
        _sender = _client.CreateSender(_queueName);
        _receiver = _client.CreateReceiver(_queueName);
    }

    public async Task EnqueueAsync(JobId id, CancellationToken cancellationToken = default)
    {
        var message = new ServiceBusMessage(id.Value.ToString())
        {
            MessageId = id.Value.ToString(),
            ContentType = "application/json"
        };

        await _sender.SendMessageAsync(message, cancellationToken);
    }

    public async Task<JobId?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var message = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5), cancellationToken);

        if (message == null)
            return null;

        var jobId = new JobId(Guid.Parse(message.Body.ToString()));

        // Complete the message (remove from queue)
        await _receiver.CompleteMessageAsync(message, cancellationToken);

        return jobId;
    }

    public async Task<int> GetQueueLengthAsync(CancellationToken cancellationToken = default)
    {
        // Note: Service Bus requires management client for queue metrics
        // This is a simplified version
        return 0; // Implement using ServiceBusAdministrationClient if needed
    }
}
```

**Configuration:**
```csharp
var serviceBusConnectionString = builder.Configuration.GetConnectionString("ServiceBus");

builder.Services.AddSingleton<IJobQueue>(sp => 
    new ServiceBusJobQueue(serviceBusConnectionString!, "callbackforge-jobs"));
```

---

#### SQL Server Job Store

Use SQL Server for reliable, ACID-compliant job persistence with distributed locking.

**NuGet Packages:**
```bash
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
```

**Entity and DbContext:**
```csharp
using Microsoft.EntityFrameworkCore;
using Mavusi.CallbackForge.Models;

public class JobEntity
{
    public Guid Id { get; set; }
    public string? IdempotencyKey { get; set; }
    public string RequestMethod { get; set; } = string.Empty;
    public string RequestUrl { get; set; } = string.Empty;
    public string? RequestBody { get; set; }
    public string? RequestHeaders { get; set; } // JSON serialized
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ResponseHeaders { get; set; }
    public string? CallbackUrl { get; set; }
    public string? CallbackMethod { get; set; }
    public string CallbackStatus { get; set; } = string.Empty;
    public int CallbackAttemptCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? LockedBy { get; set; }
}

public class JobDbContext : DbContext
{
    public JobDbContext(DbContextOptions<JobDbContext> options) : base(options) { }

    public DbSet<JobEntity> Jobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.NextRetryAt);
            entity.Property(e => e.RequestUrl).HasMaxLength(2000);
            entity.Property(e => e.Status).HasMaxLength(50);
        });
    }
}
```

**Implementation:**
```csharp
using Microsoft.EntityFrameworkCore;
using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;
using System.Text.Json;

public class SqlJobStore : IJobStore
{
    private readonly JobDbContext _context;

    public SqlJobStore(JobDbContext context)
    {
        _context = context;
    }

    public async Task<Job> CreateAsync(Job job, CancellationToken cancellationToken = default)
    {
        var entity = new JobEntity
        {
            Id = job.Id.Value,
            IdempotencyKey = job.IdempotencyKey,
            RequestMethod = job.Request.Method,
            RequestUrl = job.Request.Url,
            RequestBody = job.Request.Body,
            RequestHeaders = JsonSerializer.Serialize(job.Request.Headers),
            Status = job.Status.ToString(),
            AttemptCount = job.AttemptCount,
            CreatedAt = job.CreatedAt,
            CompletedAt = job.CompletedAt,
            NextRetryAt = job.NextRetryAt,
            CallbackUrl = job.CallbackInfo?.Url,
            CallbackMethod = job.CallbackInfo?.Method,
            CallbackStatus = job.CallbackInfo?.Status.ToString() ?? "Pending",
            CallbackAttemptCount = job.CallbackInfo?.AttemptCount ?? 0
        };

        _context.Jobs.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<Job?> GetByIdAsync(JobId id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Jobs.FindAsync(new object[] { id.Value }, cancellationToken);
        return entity != null ? MapToJob(entity) : null;
    }

    public async Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Jobs
            .FirstOrDefaultAsync(j => j.IdempotencyKey == idempotencyKey, cancellationToken);
        return entity != null ? MapToJob(entity) : null;
    }

    public async Task UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Jobs.FindAsync(new object[] { job.Id.Value }, cancellationToken);
        if (entity != null)
        {
            entity.Status = job.Status.ToString();
            entity.AttemptCount = job.AttemptCount;
            entity.CompletedAt = job.CompletedAt;
            entity.NextRetryAt = job.NextRetryAt;
            entity.ResponseStatusCode = job.Response?.StatusCode;
            entity.ResponseBody = job.Response?.Body;
            entity.ResponseHeaders = job.Response?.Headers != null 
                ? JsonSerializer.Serialize(job.Response.Headers) 
                : null;
            entity.CallbackStatus = job.CallbackInfo?.Status.ToString() ?? "Pending";
            entity.CallbackAttemptCount = job.CallbackInfo?.AttemptCount ?? 0;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> TryAcquireLockAsync(JobId id, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(lockDuration);

        var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
            @"UPDATE Jobs 
              SET LockedUntil = {0}, LockedBy = {1}
              WHERE Id = {2} AND (LockedUntil IS NULL OR LockedUntil < {3})",
            expiresAt,
            Environment.MachineName,
            id.Value,
            now,
            cancellationToken);

        return rowsAffected > 0;
    }

    public async Task ReleaseLockAsync(JobId id, CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Jobs SET LockedUntil = NULL, LockedBy = NULL WHERE Id = {0}",
            id.Value,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Job>> GetJobsReadyForRetryAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entities = await _context.Jobs
            .Where(j => j.Status == "Failed" && j.NextRetryAt != null && j.NextRetryAt <= now)
            .OrderBy(j => j.NextRetryAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToJob).ToList();
    }

    private Job MapToJob(JobEntity entity)
    {
        var job = new Job(
            new JobId(entity.Id),
            new HttpRequest(entity.RequestMethod, entity.RequestUrl, entity.RequestBody,
                JsonSerializer.Deserialize<Dictionary<string, string>>(entity.RequestHeaders ?? "{}") ?? new()),
            entity.IdempotencyKey);

        // Set status and other properties via reflection or make Job mutable
        typeof(Job).GetProperty(nameof(Job.Status))!.SetValue(job, Enum.Parse<JobStatus>(entity.Status));
        typeof(Job).GetProperty(nameof(Job.AttemptCount))!.SetValue(job, entity.AttemptCount);
        typeof(Job).GetProperty(nameof(Job.CreatedAt))!.SetValue(job, entity.CreatedAt);
        typeof(Job).GetProperty(nameof(Job.CompletedAt))!.SetValue(job, entity.CompletedAt);
        typeof(Job).GetProperty(nameof(Job.NextRetryAt))!.SetValue(job, entity.NextRetryAt);

        if (entity.ResponseStatusCode.HasValue)
        {
            var response = new HttpResponse(
                entity.ResponseStatusCode.Value,
                entity.ResponseBody,
                JsonSerializer.Deserialize<Dictionary<string, string>>(entity.ResponseHeaders ?? "{}") ?? new());
            typeof(Job).GetProperty(nameof(Job.Response))!.SetValue(job, response);
        }

        if (!string.IsNullOrEmpty(entity.CallbackUrl))
        {
            var callbackInfo = new CallbackInfo(entity.CallbackUrl, entity.CallbackMethod ?? "POST");
            typeof(CallbackInfo).GetProperty(nameof(CallbackInfo.Status))!.SetValue(callbackInfo, 
                Enum.Parse<CallbackStatus>(entity.CallbackStatus));
            typeof(CallbackInfo).GetProperty(nameof(CallbackInfo.AttemptCount))!.SetValue(callbackInfo, 
                entity.CallbackAttemptCount);
            typeof(Job).GetProperty(nameof(Job.CallbackInfo))!.SetValue(job, callbackInfo);
        }

        return job;
    }
}
```

**Configuration:**
```csharp
builder.Services.AddDbContext<JobDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("JobDatabase")));

builder.Services.AddScoped<IJobStore, SqlJobStore>();
```

**Connection String (appsettings.json):**
```json
{
  "ConnectionStrings": {
    "JobDatabase": "Server=localhost;Database=CallbackForge;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

---

#### PostgreSQL Job Store

Use PostgreSQL for open-source, production-grade relational storage.

**NuGet Packages:**
```bash
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design
```

**Implementation:**
Use the same `JobEntity` and `JobDbContext` classes from SQL Server above, then configure PostgreSQL provider:

```csharp
builder.Services.AddDbContext<JobDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("JobDatabase")));

builder.Services.AddScoped<IJobStore, SqlJobStore>(); // Same implementation works
```

**Connection String (appsettings.json):**
```json
{
  "ConnectionStrings": {
    "JobDatabase": "Host=localhost;Database=callbackforge;Username=postgres;Password=yourpassword"
  }
}
```

---

#### MongoDB Job Store

Use MongoDB for flexible, document-based storage with horizontal scaling capabilities.

**NuGet Package:**
```bash
dotnet add package MongoDB.Driver
```

**Implementation:**
```csharp
using MongoDB.Driver;
using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class JobDocument
{
    [BsonId]
    public Guid Id { get; set; }
    public string? IdempotencyKey { get; set; }
    public BsonDocument Request { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public BsonDocument? Response { get; set; }
    public BsonDocument? CallbackInfo { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? LockedBy { get; set; }
}

public class MongoJobStore : IJobStore
{
    private readonly IMongoCollection<JobDocument> _collection;

    public MongoJobStore(IMongoClient client, string databaseName = "callbackforge", string collectionName = "jobs")
    {
        var database = client.GetDatabase(databaseName);
        _collection = database.GetCollection<JobDocument>(collectionName);

        // Create indexes
        var indexKeys = Builders<JobDocument>.IndexKeys;
        _collection.Indexes.CreateOne(new CreateIndexModel<JobDocument>(
            indexKeys.Ascending(j => j.IdempotencyKey),
            new CreateIndexOptions { Unique = true, Sparse = true }));
        _collection.Indexes.CreateOne(new CreateIndexModel<JobDocument>(
            indexKeys.Ascending(j => j.Status)));
        _collection.Indexes.CreateOne(new CreateIndexModel<JobDocument>(
            indexKeys.Ascending(j => j.NextRetryAt)));
    }

    public async Task<Job> CreateAsync(Job job, CancellationToken cancellationToken = default)
    {
        var document = new JobDocument
        {
            Id = job.Id.Value,
            IdempotencyKey = job.IdempotencyKey,
            Request = new BsonDocument
            {
                { "method", job.Request.Method },
                { "url", job.Request.Url },
                { "body", job.Request.Body ?? BsonNull.Value },
                { "headers", new BsonDocument(job.Request.Headers.Select(kvp => new BsonElement(kvp.Key, kvp.Value))) }
            },
            Status = job.Status.ToString(),
            AttemptCount = job.AttemptCount,
            CreatedAt = job.CreatedAt,
            CompletedAt = job.CompletedAt,
            NextRetryAt = job.NextRetryAt
        };

        if (job.CallbackInfo != null)
        {
            document.CallbackInfo = new BsonDocument
            {
                { "url", job.CallbackInfo.Url },
                { "method", job.CallbackInfo.Method },
                { "status", job.CallbackInfo.Status.ToString() },
                { "attemptCount", job.CallbackInfo.AttemptCount }
            };
        }

        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        return job;
    }

    public async Task<Job?> GetByIdAsync(JobId id, CancellationToken cancellationToken = default)
    {
        var document = await _collection
            .Find(j => j.Id == id.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return document != null ? MapToJob(document) : null;
    }

    public async Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var document = await _collection
            .Find(j => j.IdempotencyKey == idempotencyKey)
            .FirstOrDefaultAsync(cancellationToken);

        return document != null ? MapToJob(document) : null;
    }

    public async Task UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        var update = Builders<JobDocument>.Update
            .Set(j => j.Status, job.Status.ToString())
            .Set(j => j.AttemptCount, job.AttemptCount)
            .Set(j => j.CompletedAt, job.CompletedAt)
            .Set(j => j.NextRetryAt, job.NextRetryAt);

        if (job.Response != null)
        {
            update = update.Set(j => j.Response, new BsonDocument
            {
                { "statusCode", job.Response.StatusCode },
                { "body", job.Response.Body ?? BsonNull.Value },
                { "headers", new BsonDocument(job.Response.Headers.Select(kvp => new BsonElement(kvp.Key, kvp.Value))) }
            });
        }

        if (job.CallbackInfo != null)
        {
            update = update.Set(j => j.CallbackInfo, new BsonDocument
            {
                { "url", job.CallbackInfo.Url },
                { "method", job.CallbackInfo.Method },
                { "status", job.CallbackInfo.Status.ToString() },
                { "attemptCount", job.CallbackInfo.AttemptCount }
            });
        }

        await _collection.UpdateOneAsync(
            j => j.Id == job.Id.Value,
            update,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> TryAcquireLockAsync(JobId id, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(lockDuration);

        var filter = Builders<JobDocument>.Filter.And(
            Builders<JobDocument>.Filter.Eq(j => j.Id, id.Value),
            Builders<JobDocument>.Filter.Or(
                Builders<JobDocument>.Filter.Eq(j => j.LockedUntil, null),
                Builders<JobDocument>.Filter.Lt(j => j.LockedUntil, now)
            )
        );

        var update = Builders<JobDocument>.Update
            .Set(j => j.LockedUntil, expiresAt)
            .Set(j => j.LockedBy, Environment.MachineName);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task ReleaseLockAsync(JobId id, CancellationToken cancellationToken = default)
    {
        var update = Builders<JobDocument>.Update
            .Set(j => j.LockedUntil, null)
            .Set(j => j.LockedBy, null);

        await _collection.UpdateOneAsync(
            j => j.Id == id.Value,
            update,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Job>> GetJobsReadyForRetryAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var documents = await _collection
            .Find(j => j.Status == "Failed" && j.NextRetryAt != null && j.NextRetryAt <= now)
            .SortBy(j => j.NextRetryAt)
            .Limit(maxCount)
            .ToListAsync(cancellationToken);

        return documents.Select(MapToJob).ToList();
    }

    private Job MapToJob(JobDocument doc)
    {
        var headers = doc.Request["headers"].AsBsonDocument
            .ToDictionary(e => e.Name, e => e.Value.AsString);

        var job = new Job(
            new JobId(doc.Id),
            new HttpRequest(
                doc.Request["method"].AsString,
                doc.Request["url"].AsString,
                doc.Request["body"].IsBsonNull ? null : doc.Request["body"].AsString,
                headers),
            doc.IdempotencyKey);

        typeof(Job).GetProperty(nameof(Job.Status))!.SetValue(job, Enum.Parse<JobStatus>(doc.Status));
        typeof(Job).GetProperty(nameof(Job.AttemptCount))!.SetValue(job, doc.AttemptCount);
        typeof(Job).GetProperty(nameof(Job.CreatedAt))!.SetValue(job, doc.CreatedAt);
        typeof(Job).GetProperty(nameof(Job.CompletedAt))!.SetValue(job, doc.CompletedAt);
        typeof(Job).GetProperty(nameof(Job.NextRetryAt))!.SetValue(job, doc.NextRetryAt);

        if (doc.Response != null)
        {
            var responseHeaders = doc.Response["headers"].AsBsonDocument
                .ToDictionary(e => e.Name, e => e.Value.AsString);

            var response = new HttpResponse(
                doc.Response["statusCode"].AsInt32,
                doc.Response["body"].IsBsonNull ? null : doc.Response["body"].AsString,
                responseHeaders);
            typeof(Job).GetProperty(nameof(Job.Response))!.SetValue(job, response);
        }

        if (doc.CallbackInfo != null)
        {
            var callbackInfo = new CallbackInfo(
                doc.CallbackInfo["url"].AsString,
                doc.CallbackInfo["method"].AsString);
            typeof(CallbackInfo).GetProperty(nameof(CallbackInfo.Status))!.SetValue(callbackInfo,
                Enum.Parse<CallbackStatus>(doc.CallbackInfo["status"].AsString));
            typeof(CallbackInfo).GetProperty(nameof(CallbackInfo.AttemptCount))!.SetValue(callbackInfo,
                doc.CallbackInfo["attemptCount"].AsInt32);
            typeof(Job).GetProperty(nameof(Job.CallbackInfo))!.SetValue(job, callbackInfo);
        }

        return job;
    }
}
```

**Configuration:**
```csharp
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("MongoDB");
    return new MongoClient(connectionString);
});

builder.Services.AddSingleton<IJobStore, MongoJobStore>();
```

**Connection String (appsettings.json):**
```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"
  }
}
```

---

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

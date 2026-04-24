# Architecture & Extension Guide

## System Architecture

CallbackForge is built with a layered, interface-driven architecture that separates concerns and enables easy customization.

```
┌─────────────────────────────────────────────────────────────┐
│                    Ingestion Layer                          │
│  ┌──────────────────┐         ┌──────────────────┐         │
│  │ HttpFlowClient   │────────▶│   IJobStore      │         │
│  └──────────────────┘         └──────────────────┘         │
│           │                              │                  │
│           ▼                              │                  │
│  ┌──────────────────┐                   │                  │
│  │   IJobQueue      │                   │                  │
│  └──────────────────┘                   │                  │
└────────────│────────────────────────────┼──────────────────┘
             │                             │
             │                             │
┌────────────▼─────────────────────────────▼──────────────────┐
│                    Worker Layer                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              HttpFlowWorker                          │   │
│  │  (Background Service - Dequeues & Processes Jobs)   │   │
│  └──────────────────────────────────────────────────────┘   │
│                           │                                  │
│                           ▼                                  │
│  ┌──────────────────────────────────────────────────────┐   │
│  │            Execution Pipeline                        │   │
│  │  ┌────────────────────────────────────────────────┐  │   │
│  │  │ 1. PrepareRequestStep                          │  │   │
│  │  │ 2. IdempotencyCheckStep                        │  │   │
│  │  │ 3. HttpExecutionStep                           │  │   │
│  │  │ 4. RetryStep                                   │  │   │
│  │  │ 5. PersistResultStep                           │  │   │
│  │  │ 6. CallbackDispatchStep                        │  │   │
│  │  └────────────────────────────────────────────────┘  │   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────┘
             │                             │
             │                             │
┌────────────▼─────────────────────────────▼──────────────────┐
│                  Support Workers                            │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  RetrySchedulerWorker                                │   │
│  │  (Scans for jobs ready for retry)                   │   │
│  └──────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  CallbackRetryWorker                                 │   │
│  │  (Retries failed callback deliveries)               │   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────┘
```

## Core Interfaces

### IJobStore

Manages job persistence and distributed locking.

```csharp
public interface IJobStore
{
    Task<Job> CreateAsync(Job job, CancellationToken cancellationToken = default);
    Task<Job?> GetByIdAsync(JobId id, CancellationToken cancellationToken = default);
    Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task UpdateAsync(Job job, CancellationToken cancellationToken = default);
    Task<bool> TryAcquireLockAsync(JobId id, TimeSpan lockDuration, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(JobId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Job>> GetJobsReadyForRetryAsync(int maxCount, CancellationToken cancellationToken = default);
}
```

### IJobQueue

Handles job queuing and dequeuing.

```csharp
public interface IJobQueue
{
    Task EnqueueAsync(JobId id, CancellationToken cancellationToken = default);
    Task<JobId?> DequeueAsync(CancellationToken cancellationToken = default);
    Task<int> GetQueueLengthAsync(CancellationToken cancellationToken = default);
}
```

### IExecutionStep

Composable pipeline steps for job processing.

```csharp
public interface IExecutionStep
{
    Task ExecuteAsync(JobContext context);
}
```

### IRetryScheduler

Manages retry logic and scheduling.

```csharp
public interface IRetryScheduler
{
    Task ScheduleRetryAsync(Job job, CancellationToken cancellationToken = default);
    DateTime CalculateNextAttempt(int attemptNumber);
}
```

### ICallbackDispatcher

Handles callback delivery to external systems.

```csharp
public interface ICallbackDispatcher
{
    Task DispatchAsync(Job job, CancellationToken cancellationToken = default);
}
```

---

## Custom Implementations

### Redis Job Queue

For production use with multiple worker instances:

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
        await db.ListRightPushAsync(_queueKey, id.ToString());
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

### SQL Job Store

Using Entity Framework Core:

```csharp
using Microsoft.EntityFrameworkCore;
using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

public class SqlJobStore : IJobStore
{
    private readonly JobDbContext _context;

    public SqlJobStore(JobDbContext context)
    {
        _context = context;
    }

    public async Task<Job> CreateAsync(Job job, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(job);
        _context.Jobs.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return job;
    }

    public async Task<Job?> GetByIdAsync(JobId id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Jobs
            .FirstOrDefaultAsync(j => j.Id == id.Value, cancellationToken);
        return entity != null ? MapToModel(entity) : null;
    }

    public async Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Jobs
            .FirstOrDefaultAsync(j => j.IdempotencyKey == idempotencyKey, cancellationToken);
        return entity != null ? MapToModel(entity) : null;
    }

    public async Task UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Jobs.FindAsync(new object[] { job.Id.Value }, cancellationToken);
        if (entity != null)
        {
            UpdateEntity(entity, job);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> TryAcquireLockAsync(JobId id, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(lockDuration);

        // Use raw SQL for atomic lock acquisition
        var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
            @"UPDATE Jobs 
              SET LockedUntil = {0}, LockedBy = {1}
              WHERE Id = {2} AND (LockedUntil IS NULL OR LockedUntil < {3})",
            expiresAt,
            Environment.MachineName,
            id.Value,
            now,
            cancellationToken
        );

        return rowsAffected > 0;
    }

    public async Task ReleaseLockAsync(JobId id, CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Jobs SET LockedUntil = NULL, LockedBy = NULL WHERE Id = {0}",
            id.Value,
            cancellationToken
        );
    }

    public async Task<IReadOnlyList<Job>> GetJobsReadyForRetryAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entities = await _context.Jobs
            .Where(j => j.NextAttemptAt.HasValue && j.NextAttemptAt.Value <= now)
            .Where(j => j.Status == (int)JobStatus.Failed || j.Status == (int)JobStatus.Pending)
            .OrderBy(j => j.NextAttemptAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToModel).ToList();
    }

    // Mapping methods omitted for brevity
}
```

### MongoDB Job Store

Using MongoDB driver:

```csharp
using MongoDB.Driver;
using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

public class MongoJobStore : IJobStore
{
    private readonly IMongoCollection<JobDocument> _collection;

    public MongoJobStore(IMongoDatabase database)
    {
        _collection = database.GetCollection<JobDocument>("jobs");

        // Create indexes
        _collection.Indexes.CreateOne(
            new CreateIndexModel<JobDocument>(
                Builders<JobDocument>.IndexKeys.Ascending(j => j.IdempotencyKey),
                new CreateIndexOptions { Unique = true, Sparse = true }
            )
        );
    }

    public async Task<Job> CreateAsync(Job job, CancellationToken cancellationToken = default)
    {
        var document = MapToDocument(job);
        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        return job;
    }

    public async Task<Job?> GetByIdAsync(JobId id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<JobDocument>.Filter.Eq(j => j.Id, id.Value);
        var document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document != null ? MapToModel(document) : null;
    }

    public async Task<Job?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var filter = Builders<JobDocument>.Filter.Eq(j => j.IdempotencyKey, idempotencyKey);
        var document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return document != null ? MapToModel(document) : null;
    }

    public async Task UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        var filter = Builders<JobDocument>.Filter.Eq(j => j.Id, job.Id.Value);
        var document = MapToDocument(job);
        await _collection.ReplaceOneAsync(filter, document, cancellationToken: cancellationToken);
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
        var filter = Builders<JobDocument>.Filter.Eq(j => j.Id, id.Value);
        var update = Builders<JobDocument>.Update
            .Set(j => j.LockedUntil, null)
            .Set(j => j.LockedBy, null);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Job>> GetJobsReadyForRetryAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<JobDocument>.Filter.And(
            Builders<JobDocument>.Filter.Ne(j => j.NextAttemptAt, null),
            Builders<JobDocument>.Filter.Lte(j => j.NextAttemptAt, now),
            Builders<JobDocument>.Filter.In(j => j.Status, new[] { (int)JobStatus.Failed, (int)JobStatus.Pending })
        );

        var documents = await _collection
            .Find(filter)
            .SortBy(j => j.NextAttemptAt)
            .Limit(maxCount)
            .ToListAsync(cancellationToken);

        return documents.Select(MapToModel).ToList();
    }

    // Mapping methods omitted for brevity
}
```

### Custom Execution Step

Add custom business logic to the pipeline:

```csharp
using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

public class AuditLoggingStep : IExecutionStep
{
    private readonly IAuditLogger _auditLogger;

    public AuditLoggingStep(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    public async Task ExecuteAsync(JobContext context)
    {
        await _auditLogger.LogAsync(new AuditEntry
        {
            JobId = context.Job.Id,
            Action = "JobProcessed",
            Status = context.Job.Status,
            Timestamp = DateTime.UtcNow,
            Details = new
            {
                Url = context.Job.Request.Url,
                Method = context.Job.Request.Method,
                Attempts = context.Job.Attempts,
                Duration = context.Job.Response?.Duration
            }
        });
    }
}

// Register in pipeline
services.AddTransient<AuditLoggingStep>();
services.AddSingleton<ExecutionPipeline>(sp => new ExecutionPipeline(new IExecutionStep[]
{
    sp.GetRequiredService<PrepareRequestStep>(),
    sp.GetRequiredService<IdempotencyCheckStep>(),
    sp.GetRequiredService<HttpExecutionStep>(),
    sp.GetRequiredService<RetryStep>(),
    sp.GetRequiredService<PersistResultStep>(),
    sp.GetRequiredService<AuditLoggingStep>(), // Custom step
    sp.GetRequiredService<CallbackDispatchStep>()
}));
```

### Custom Retry Strategy

Implement a different retry algorithm:

```csharp
public class LinearBackoffRetryScheduler : IRetryScheduler
{
    private readonly IJobStore _jobStore;
    private readonly IJobQueue _jobQueue;
    private readonly int _maxAttempts;
    private readonly TimeSpan _fixedDelay;

    public LinearBackoffRetryScheduler(
        IJobStore jobStore,
        IJobQueue jobQueue,
        int maxAttempts = 5,
        TimeSpan? fixedDelay = null)
    {
        _jobStore = jobStore;
        _jobQueue = jobQueue;
        _maxAttempts = maxAttempts;
        _fixedDelay = fixedDelay ?? TimeSpan.FromSeconds(10);
    }

    public async Task ScheduleRetryAsync(Job job, CancellationToken cancellationToken = default)
    {
        if (job.Attempts >= _maxAttempts)
        {
            job.Status = JobStatus.Failed;
            job.FailureReason = $"Max retry attempts ({_maxAttempts}) reached";
            await _jobStore.UpdateAsync(job, cancellationToken);
            return;
        }

        job.NextAttemptAt = CalculateNextAttempt(job.Attempts);
        job.Status = JobStatus.Pending;
        await _jobStore.UpdateAsync(job, cancellationToken);
        await _jobQueue.EnqueueAsync(job.Id, cancellationToken);
    }

    public DateTime CalculateNextAttempt(int attemptNumber)
    {
        // Linear backoff: same delay for each attempt
        return DateTime.UtcNow.Add(_fixedDelay);
    }
}
```

## Registration

Replace default implementations in DI:

```csharp
builder.Services.AddCallbackForge(options => { /* ... */ });

// Replace with Redis queue
builder.Services.Replace(ServiceDescriptor.Singleton<IJobQueue, RedisJobQueue>());

// Replace with SQL store
builder.Services.Replace(ServiceDescriptor.Singleton<IJobStore, SqlJobStore>());

// Replace retry strategy
builder.Services.Replace(ServiceDescriptor.Singleton<IRetryScheduler, LinearBackoffRetryScheduler>());
```

## Testing Custom Implementations

```csharp
[TestClass]
public class CustomJobStoreTests
{
    [TestMethod]
    public async Task CustomStore_Should_PersistJob()
    {
        // Arrange
        var store = new SqlJobStore(dbContext);
        var job = new Job
        {
            Id = JobId.New(),
            Status = JobStatus.Pending,
            Request = new HttpRequest
            {
                Url = "https://api.example.com",
                Method = "GET"
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await store.CreateAsync(job);
        var retrieved = await store.GetByIdAsync(job.Id);

        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual(job.Id, retrieved.Id);
        Assert.AreEqual(JobStatus.Pending, retrieved.Status);
    }
}
```

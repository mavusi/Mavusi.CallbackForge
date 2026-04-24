# Changelog

All notable changes to CallbackForge will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0-alpha] - 2024-01-15

### Added

#### Core Features
- **Asynchronous Job Processing** - Submit HTTP requests and process in background
- **Callback System** - Automatic callback delivery when jobs complete
- **Retry Mechanism** - Exponential backoff with jitter for failed requests
- **Idempotency** - Prevent duplicate job execution with idempotency keys
- **Distributed Locking** - Safe job processing across multiple instances

#### Components

##### Models
- `JobId` - Strong-typed job identifier
- `Job` - Core job entity with full state tracking
- `JobStatus` - Job lifecycle states (Pending, Processing, Completed, Failed, Cancelled)
- `JobContext` - Execution context for pipeline
- `HttpRequest` - HTTP request configuration
- `HttpResponse` - HTTP response capture
- `CallbackInfo` - Callback configuration with retry tracking
- `CallbackStatus` - Callback delivery states

##### Abstractions
- `IJobStore` - Job persistence and locking contract
- `IJobQueue` - Queue operations contract
- `IExecutionStep` - Pipeline step contract
- `IRetryScheduler` - Retry logic contract
- `ICallbackDispatcher` - Callback delivery contract

##### Infrastructure
- `InMemoryJobQueue` - Channel-based queue for development
- `InMemoryJobStore` - Concurrent dictionary-based storage for development
- `ExponentialBackoffRetryScheduler` - Smart retry with backoff and jitter
- `CallbackDispatcher` - HTTP callback delivery with retry support

##### Execution Pipeline
- `ExecutionPipeline` - Composable step orchestrator
- `PrepareRequestStep` - Job initialization
- `IdempotencyCheckStep` - Duplicate request detection
- `HttpExecutionStep` - HTTP request execution with timeout
- `RetryStep` - Failure retry scheduling
- `PersistResultStep` - Result persistence
- `CallbackDispatchStep` - Callback trigger

##### Background Workers
- `HttpFlowWorker` - Main job processing worker
- `RetrySchedulerWorker` - Retry orchestration worker
- `CallbackRetryWorker` - Callback retry handler

##### Client API
- `HttpFlowClient` - Public API for job submission and retrieval
- Request validation and error handling

##### Configuration
- `ServiceCollectionExtensions` - DI registration
- `CallbackForgeOptions` - Comprehensive configuration model
- HttpClientFactory integration

#### Documentation
- `README.md` - Quick start guide and overview
- `ARCHITECTURE.md` - Deep dive into architecture and extension points
- `API_EXAMPLES.md` - REST API usage examples
- `TROUBLESHOOTING.md` - Common issues and solutions
- `PROJECT_SUMMARY.md` - Complete project overview
- `CHECKLIST.md` - Implementation and deployment checklist
- `QUICK_REFERENCE.md` - Developer quick reference card
- `CHANGELOG.md` - Version history

#### Examples
- `Mavusi.CallbackForge.Sample` - Console application example
- `Mavusi.CallbackForge.WebApi` - ASP.NET Core Web API example
- Swagger/OpenAPI integration
- Multiple usage patterns demonstrated

### Technical Details

#### Dependencies
- Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0
- Microsoft.Extensions.Hosting.Abstractions 8.0.0
- Microsoft.Extensions.Http 8.0.0
- Microsoft.Extensions.Logging.Abstractions 8.0.0

#### Target Framework
- .NET 8.0

#### Language Version
- C# 12.0

### Known Limitations

- In-memory implementations not suitable for production
- No distributed queue implementation included
- No persistent storage implementation included
- No built-in authentication for callbacks
- No rate limiting for outbound requests
- No dead letter queue implementation

### Breaking Changes
None (initial release)

---

## [Unreleased]

### Planned for 1.0.0-beta

#### To Add
- Redis queue implementation
- SQL Server job store implementation
- Distributed lock provider (Redis)
- Health checks
- Metrics/telemetry
- Circuit breaker support
- Dead letter queue

#### To Improve
- Performance optimizations
- Memory usage optimizations
- Better error messages
- Enhanced logging

---

## Version History

- **1.0.0-alpha** (2024-01-15) - Initial release
  - Core functionality complete
  - Development implementations
  - Comprehensive documentation
  - Example applications

---

## Migration Guides

### From 0.x to 1.0

Not applicable (initial release).

---

## Support

For questions and support:
- Check `TROUBLESHOOTING.md`
- Review `ARCHITECTURE.md`
- See example applications
- Open GitHub issue (if applicable)

---

## Contributing

Contributions welcome! Please:
1. Read `ARCHITECTURE.md` for design principles
2. Check `CHECKLIST.md` for implementation status
3. Write tests for new features
4. Update documentation
5. Submit pull request

---

## License

MIT License

Copyright (c) 2024 Mavusi

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

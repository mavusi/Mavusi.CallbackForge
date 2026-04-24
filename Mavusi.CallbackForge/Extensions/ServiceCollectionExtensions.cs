using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Client;
using Mavusi.CallbackForge.Infrastructure.Callbacks;
using Mavusi.CallbackForge.Infrastructure.Persistence;
using Mavusi.CallbackForge.Infrastructure.Queue;
using Mavusi.CallbackForge.Infrastructure.Retry;
using Mavusi.CallbackForge.Pipeline;
using Mavusi.CallbackForge.Pipeline.Steps;
using Mavusi.CallbackForge.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace Mavusi.CallbackForge.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCallbackForge(
        this IServiceCollection services,
        Action<CallbackForgeOptions>? configureOptions = null)
    {
        var options = new CallbackForgeOptions();
        configureOptions?.Invoke(options);

        services.AddSingleton<IJobStore, InMemoryJobStore>();
        services.AddSingleton<IJobQueue, InMemoryJobQueue>();

        services.AddSingleton<IRetryScheduler>(sp => new ExponentialBackoffRetryScheduler(
            sp.GetRequiredService<IJobStore>(),
            sp.GetRequiredService<IJobQueue>(),
            options.MaxRetryAttempts,
            options.BaseRetryDelay,
            options.MaxRetryDelay
        ));

        services.AddHttpClient("HttpFlowClient")
            .ConfigureHttpClient(client =>
            {
                client.Timeout = options.DefaultRequestTimeout;
            });

        services.AddHttpClient("CallbackClient")
            .ConfigureHttpClient(client =>
            {
                client.Timeout = options.CallbackTimeout;
            });

        services.AddSingleton<ICallbackDispatcher>(sp => new CallbackDispatcher(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IJobStore>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CallbackDispatcher>>(),
            options.MaxCallbackAttempts,
            options.CallbackTimeout
        ));

        services.AddTransient<PrepareRequestStep>();
        services.AddTransient<IdempotencyCheckStep>();
        services.AddTransient<HttpExecutionStep>();
        services.AddTransient<RetryStep>();
        services.AddTransient<PersistResultStep>();
        services.AddTransient<CallbackDispatchStep>();

        services.AddSingleton<ExecutionPipeline>(sp => new ExecutionPipeline(new IExecutionStep[]
        {
            sp.GetRequiredService<PrepareRequestStep>(),
            sp.GetRequiredService<IdempotencyCheckStep>(),
            sp.GetRequiredService<HttpExecutionStep>(),
            sp.GetRequiredService<RetryStep>(),
            sp.GetRequiredService<PersistResultStep>(),
            sp.GetRequiredService<CallbackDispatchStep>()
        }));

        services.AddSingleton<HttpFlowClient>();

        if (options.EnableWorkers)
        {
            services.AddHostedService(sp => new HttpFlowWorker(
                sp.GetRequiredService<IJobQueue>(),
                sp.GetRequiredService<IJobStore>(),
                sp.GetRequiredService<ExecutionPipeline>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpFlowWorker>>(),
                options.WorkerLockDuration,
                options.WorkerDequeueDelay
            ));

            services.AddHostedService(sp => new RetrySchedulerWorker(
                sp.GetRequiredService<IJobStore>(),
                sp.GetRequiredService<IJobQueue>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<RetrySchedulerWorker>>(),
                options.RetryScanInterval
            ));

            services.AddHostedService(sp => new CallbackRetryWorker(
                sp.GetRequiredService<IJobStore>(),
                sp.GetRequiredService<ICallbackDispatcher>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CallbackRetryWorker>>(),
                options.CallbackScanInterval
            ));
        }

        return services;
    }
}

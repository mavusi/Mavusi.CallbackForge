using Mavusi.CallbackForge.Client;
using Mavusi.CallbackForge.Extensions;
using Mavusi.CallbackForge.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mavusi.CallbackForge.Sample;

/// <summary>
/// Example console application demonstrating CallbackForge usage
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Configure CallbackForge with custom options
        builder.Services.AddCallbackForge(options =>
        {
            options.MaxRetryAttempts = 3;
            options.BaseRetryDelay = TimeSpan.FromSeconds(2);
            options.DefaultRequestTimeout = TimeSpan.FromSeconds(10);
            options.MaxCallbackAttempts = 2;
            options.EnableWorkers = true;
        });

        builder.Logging.SetMinimumLevel(LogLevel.Information);

        var host = builder.Build();

        // Example 1: Simple HTTP request with callback
        await SimpleExample(host.Services);

        // Example 2: Request with idempotency
        await IdempotencyExample(host.Services);

        // Example 3: Monitoring job status
        await MonitoringExample(host.Services);

        // Start the workers
        await host.StartAsync();

        Console.WriteLine("\nPress Ctrl+C to stop the workers...");
        await host.WaitForShutdownAsync();
    }

    static async Task SimpleExample(IServiceProvider services)
    {
        Console.WriteLine("=== Example 1: Simple HTTP Request with Callback ===\n");

        var client = services.GetRequiredService<HttpFlowClient>();

        var request = new HttpRequest
        {
            Url = "https://jsonplaceholder.typicode.com/posts",
            Method = "POST",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            },
            Body = """
                {
                    "title": "Sample Post",
                    "body": "This is a test post",
                    "userId": 1
                }
                """
        };

        var callback = new CallbackInfo
        {
            Url = "https://webhook.site/unique-id", // Replace with your webhook URL
            Headers = new Dictionary<string, string>
            {
                { "X-Custom-Header", "CallbackForge" }
            }
        };

        var jobId = await client.SubmitAsync(request, callback);
        Console.WriteLine($"Job submitted: {jobId}");
        Console.WriteLine("Job will be processed in the background.\n");
    }

    static async Task IdempotencyExample(IServiceProvider services)
    {
        Console.WriteLine("=== Example 2: Idempotency ===\n");

        var client = services.GetRequiredService<HttpFlowClient>();

        var request = new HttpRequest
        {
            Url = "https://jsonplaceholder.typicode.com/posts/1",
            Method = "GET",
            IdempotencyKey = "example-idempotency-key-123"
        };

        // Submit the same request twice
        var jobId1 = await client.SubmitAsync(request);
        Console.WriteLine($"First submission: {jobId1}");

        var jobId2 = await client.SubmitAsync(request);
        Console.WriteLine($"Second submission: {jobId2}");

        if (jobId1 == jobId2)
        {
            Console.WriteLine("✓ Idempotency working: Same job returned for duplicate request\n");
        }
    }

    static async Task MonitoringExample(IServiceProvider services)
    {
        Console.WriteLine("=== Example 3: Job Status Monitoring ===\n");

        var client = services.GetRequiredService<HttpFlowClient>();

        var request = new HttpRequest
        {
            Url = "https://jsonplaceholder.typicode.com/posts/1",
            Method = "GET"
        };

        var jobId = await client.SubmitAsync(request);
        Console.WriteLine($"Job submitted: {jobId}");

        // Poll for job completion
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(1000);

            var job = await client.GetJobAsync(jobId);
            if (job != null)
            {
                Console.WriteLine($"Status: {job.Status}, Attempts: {job.Attempts}");

                if (job.Status == JobStatus.Completed)
                {
                    Console.WriteLine($"✓ Job completed successfully!");
                    Console.WriteLine($"  Response Status: {job.Response?.StatusCode}");
                    Console.WriteLine($"  Duration: {job.Response?.Duration.TotalMilliseconds}ms");
                    break;
                }
                else if (job.Status == JobStatus.Failed)
                {
                    Console.WriteLine($"✗ Job failed: {job.FailureReason}");
                    break;
                }
            }
        }

        Console.WriteLine();
    }
}

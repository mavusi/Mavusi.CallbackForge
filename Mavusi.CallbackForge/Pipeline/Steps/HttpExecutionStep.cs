using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Pipeline.Steps;

public sealed class HttpExecutionStep : IExecutionStep
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpExecutionStep(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task ExecuteAsync(JobContext context)
    {
        if (context.Metadata.ContainsKey("IdempotencyMatch"))
        {
            return;
        }

        var job = context.Job;
        var request = job.Request;

        var httpClient = _httpClientFactory.CreateClient("HttpFlowClient");

        if (request.Timeout.HasValue)
        {
            httpClient.Timeout = request.Timeout.Value;
        }

        var httpRequestMessage = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);

        foreach (var header in request.Headers)
        {
            httpRequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(request.Body))
        {
            httpRequestMessage.Content = new StringContent(
                request.Body,
                Encoding.UTF8,
                request.Headers.TryGetValue("Content-Type", out var contentType) ? contentType : "application/json"
            );
        }

        var stopwatch = Stopwatch.StartNew();
        var receivedAt = DateTime.UtcNow;

        try
        {
            using var httpResponseMessage = await httpClient.SendAsync(
                httpRequestMessage, 
                HttpCompletionOption.ResponseContentRead,
                context.CancellationToken
            );

            stopwatch.Stop();

            var responseBody = await httpResponseMessage.Content.ReadAsStringAsync(context.CancellationToken);

            var headers = new Dictionary<string, string>();
            foreach (var header in httpResponseMessage.Headers.Concat(httpResponseMessage.Content.Headers))
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }

            job.Response = new HttpResponse
            {
                StatusCode = (int)httpResponseMessage.StatusCode,
                Headers = headers,
                Body = responseBody,
                Duration = stopwatch.Elapsed,
                ReceivedAt = receivedAt
            };

            context.Metadata["HttpSuccess"] = httpResponseMessage.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            job.FailureReason = $"HTTP request failed: {ex.Message}";
            context.Metadata["HttpSuccess"] = false;
            context.Metadata["Exception"] = ex;
        }
        catch (TaskCanceledException ex) when (!context.CancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            job.FailureReason = $"Request timeout after {stopwatch.Elapsed}";
            context.Metadata["HttpSuccess"] = false;
            context.Metadata["Exception"] = ex;
        }
    }
}

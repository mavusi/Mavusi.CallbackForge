using System.Text;
using System.Text.Json;
using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;
using Microsoft.Extensions.Logging;

namespace Mavusi.CallbackForge.Infrastructure.Callbacks;

public sealed class CallbackDispatcher : ICallbackDispatcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJobStore _jobStore;
    private readonly ILogger<CallbackDispatcher> _logger;
    private readonly int _maxCallbackAttempts;
    private readonly TimeSpan _callbackTimeout;

    public CallbackDispatcher(
        IHttpClientFactory httpClientFactory,
        IJobStore jobStore,
        ILogger<CallbackDispatcher> logger,
        int maxCallbackAttempts = 3,
        TimeSpan? callbackTimeout = null)
    {
        _httpClientFactory = httpClientFactory;
        _jobStore = jobStore;
        _logger = logger;
        _maxCallbackAttempts = maxCallbackAttempts;
        _callbackTimeout = callbackTimeout ?? TimeSpan.FromSeconds(30);
    }

    public async Task DispatchAsync(Job job, CancellationToken cancellationToken = default)
    {
        if (job.Callback == null)
        {
            return;
        }

        var callback = job.Callback;

        if (callback.Status == CallbackStatus.Completed)
        {
            return;
        }

        if (callback.Attempts >= _maxCallbackAttempts)
        {
            callback.Status = CallbackStatus.Failed;
            callback.FailureReason = $"Max callback attempts ({_maxCallbackAttempts}) reached";
            await _jobStore.UpdateAsync(job, cancellationToken);
            _logger.LogWarning("Callback failed for job {JobId} after {Attempts} attempts", job.Id, callback.Attempts);
            return;
        }

        callback.Status = CallbackStatus.InProgress;
        callback.Attempts++;
        callback.LastAttemptAt = DateTime.UtcNow;

        var httpClient = _httpClientFactory.CreateClient("CallbackClient");
        httpClient.Timeout = _callbackTimeout;

        var payload = new
        {
            jobId = job.Id.ToString(),
            status = job.Status.ToString(),
            request = new
            {
                url = job.Request.Url,
                method = job.Request.Method,
                headers = job.Request.Headers,
                body = job.Request.Body
            },
            response = job.Response == null ? null : new
            {
                statusCode = job.Response.StatusCode,
                headers = job.Response.Headers,
                body = job.Response.Body,
                duration = job.Response.Duration.TotalMilliseconds,
                receivedAt = job.Response.ReceivedAt
            },
            attempts = job.Attempts,
            createdAt = job.CreatedAt,
            completedAt = job.UpdatedAt
        };

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, callback.Url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            )
        };

        foreach (var header in callback.Headers)
        {
            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        try
        {
            using var response = await httpClient.SendAsync(requestMessage, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                callback.Status = CallbackStatus.Completed;
                callback.FailureReason = null;
                _logger.LogInformation("Callback succeeded for job {JobId} on attempt {Attempt}", job.Id, callback.Attempts);
            }
            else
            {
                callback.FailureReason = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                await ScheduleCallbackRetry(job, callback);
                _logger.LogWarning("Callback failed for job {JobId} with status {StatusCode}", job.Id, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            callback.FailureReason = ex.Message;
            await ScheduleCallbackRetry(job, callback);
            _logger.LogError(ex, "Callback exception for job {JobId}", job.Id);
        }

        await _jobStore.UpdateAsync(job, cancellationToken);
    }

    private Task ScheduleCallbackRetry(Job job, CallbackInfo callback)
    {
        if (callback.Attempts < _maxCallbackAttempts)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, callback.Attempts - 1) * 5);
            callback.NextAttemptAt = DateTime.UtcNow.Add(delay);
            callback.Status = CallbackStatus.Pending;
        }
        else
        {
            callback.Status = CallbackStatus.Failed;
        }

        return Task.CompletedTask;
    }
}

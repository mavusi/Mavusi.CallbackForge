using Mavusi.CallbackForge.Client;
using Mavusi.CallbackForge.Extensions;
using Mavusi.CallbackForge.Models;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add CallbackForge
builder.Services.AddCallbackForge(options =>
{
    options.MaxRetryAttempts = 5;
    options.BaseRetryDelay = TimeSpan.FromSeconds(5);
    options.DefaultRequestTimeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// API Endpoints

app.MapPost("/api/jobs", async (
    [FromBody] JobSubmissionRequest request,
    [FromServices] HttpFlowClient client) =>
{
    try
    {
        var httpRequest = new Mavusi.CallbackForge.Models.HttpRequest
        {
            Url = request.Url,
            Method = request.Method,
            Headers = request.Headers ?? new Dictionary<string, string>(),
            Body = request.Body,
            Timeout = request.Timeout.HasValue 
                ? TimeSpan.FromSeconds(request.Timeout.Value) 
                : null,
            IdempotencyKey = request.IdempotencyKey
        };

        CallbackInfo? callback = null;
        if (!string.IsNullOrEmpty(request.CallbackUrl))
        {
            callback = new CallbackInfo
            {
                Url = request.CallbackUrl,
                Headers = request.CallbackHeaders ?? new Dictionary<string, string>()
            };
        }

        var jobId = await client.SubmitAsync(httpRequest, callback);

        return Results.Accepted($"/api/jobs/{jobId}", new
        {
            jobId = jobId.ToString(),
            status = "pending",
            message = "Job has been submitted and will be processed in the background"
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("SubmitJob")
.WithDescription("Submit an HTTP request job for asynchronous processing")
.Produces(202)
.Produces(400);

app.MapGet("/api/jobs/{jobId:guid}", async (
    Guid jobId,
    [FromServices] HttpFlowClient client) =>
{
    var job = await client.GetJobAsync(new JobId(jobId));

    if (job == null)
    {
        return Results.NotFound(new { error = "Job not found" });
    }

    return Results.Ok(new
    {
        jobId = job.Id.ToString(),
        status = job.Status.ToString().ToLower(),
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
        callback = job.Callback == null ? null : new
        {
            url = job.Callback.Url,
            status = job.Callback.Status.ToString().ToLower(),
            attempts = job.Callback.Attempts,
            lastAttemptAt = job.Callback.LastAttemptAt,
            failureReason = job.Callback.FailureReason
        },
        attempts = job.Attempts,
        failureReason = job.FailureReason,
        createdAt = job.CreatedAt,
        updatedAt = job.UpdatedAt
    });
})
.WithName("GetJob")
.WithDescription("Get the status and details of a submitted job")
.Produces(200)
.Produces(404);

app.MapPost("/api/webhooks/callback", async ([FromBody] dynamic payload) =>
{
    // Example webhook endpoint for receiving callbacks
    Console.WriteLine($"Received callback: {payload}");

    // Process the callback
    // In a real application, you would:
    // 1. Validate the callback signature/authentication
    // 2. Store the result in your database
    // 3. Trigger any downstream processes
    // 4. Notify users/systems

    return Results.Ok(new { message = "Callback received" });
})
.WithName("ReceiveCallback")
.WithDescription("Example webhook endpoint for receiving job completion callbacks")
.Produces(200);

app.Run();

// DTOs

public record JobSubmissionRequest(
    string Url,
    string Method,
    Dictionary<string, string>? Headers,
    string? Body,
    double? Timeout,
    string? IdempotencyKey,
    string? CallbackUrl,
    Dictionary<string, string>? CallbackHeaders
);

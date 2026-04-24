namespace Mavusi.CallbackForge.Models;

public sealed class Job
{
    public required JobId Id { get; init; }
    public JobStatus Status { get; set; }
    public required HttpRequest Request { get; init; }
    public HttpResponse? Response { get; set; }
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public CallbackInfo? Callback { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }
    public string? FailureReason { get; set; }
}

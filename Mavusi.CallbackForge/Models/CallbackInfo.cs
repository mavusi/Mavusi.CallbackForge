namespace Mavusi.CallbackForge.Models;

public sealed class CallbackInfo
{
    public required string Url { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();
    public CallbackStatus Status { get; set; } = CallbackStatus.Pending;
    public int Attempts { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? FailureReason { get; set; }
}

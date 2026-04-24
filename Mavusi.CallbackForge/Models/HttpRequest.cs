namespace Mavusi.CallbackForge.Models;

public sealed class HttpRequest
{
    public required string Url { get; init; }
    public required string Method { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();
    public string? Body { get; init; }
    public TimeSpan? Timeout { get; init; }
    public string? IdempotencyKey { get; init; }
}

namespace Mavusi.CallbackForge.Models;

public sealed class HttpResponse
{
    public int StatusCode { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();
    public string? Body { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTime ReceivedAt { get; init; }
}

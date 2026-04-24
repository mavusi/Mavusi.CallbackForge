namespace Mavusi.CallbackForge.Models;

public sealed class JobContext
{
    public required Job Job { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

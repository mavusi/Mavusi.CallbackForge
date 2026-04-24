namespace Mavusi.CallbackForge.Extensions;

public sealed class CallbackForgeOptions
{
    public bool EnableWorkers { get; set; } = true;
    public int MaxRetryAttempts { get; set; } = 5;
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan DefaultRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxCallbackAttempts { get; set; } = 3;
    public TimeSpan CallbackTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan WorkerLockDuration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan WorkerDequeueDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan RetryScanInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CallbackScanInterval { get; set; } = TimeSpan.FromSeconds(30);
}

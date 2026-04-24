using System.Threading.Channels;
using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Infrastructure.Queue;

public sealed class InMemoryJobQueue : IJobQueue
{
    private readonly Channel<JobId> _channel;

    public InMemoryJobQueue(int capacity = 1000)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<JobId>(options);
    }

    public async Task EnqueueAsync(JobId id, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(id, cancellationToken);
    }

    public async Task<JobId?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public Task<int> GetQueueLengthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_channel.Reader.Count);
    }
}

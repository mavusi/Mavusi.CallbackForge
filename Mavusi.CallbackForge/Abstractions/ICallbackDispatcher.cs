using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Abstractions;

public interface ICallbackDispatcher
{
    Task DispatchAsync(Job job, CancellationToken cancellationToken = default);
}

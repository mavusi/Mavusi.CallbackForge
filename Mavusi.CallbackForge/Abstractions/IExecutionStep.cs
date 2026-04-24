using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Abstractions;

public interface IExecutionStep
{
    Task ExecuteAsync(JobContext context);
}

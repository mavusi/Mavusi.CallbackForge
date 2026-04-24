using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Pipeline.Steps;

public sealed class CallbackDispatchStep : IExecutionStep
{
    private readonly ICallbackDispatcher _callbackDispatcher;

    public CallbackDispatchStep(ICallbackDispatcher callbackDispatcher)
    {
        _callbackDispatcher = callbackDispatcher;
    }

    public async Task ExecuteAsync(JobContext context)
    {
        var job = context.Job;

        if (job.Status == JobStatus.Completed && job.Callback != null)
        {
            await _callbackDispatcher.DispatchAsync(job, context.CancellationToken);
        }
    }
}

using Mavusi.CallbackForge.Abstractions;
using Mavusi.CallbackForge.Models;

namespace Mavusi.CallbackForge.Pipeline;

public sealed class ExecutionPipeline
{
    private readonly IReadOnlyList<IExecutionStep> _steps;

    public ExecutionPipeline(IEnumerable<IExecutionStep> steps)
    {
        _steps = steps.ToList();
    }

    public async Task ExecuteAsync(Job job, CancellationToken cancellationToken = default)
    {
        var context = new JobContext
        {
            Job = job,
            CancellationToken = cancellationToken
        };

        foreach (var step in _steps)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await step.ExecuteAsync(context);
            }
            catch (Exception ex)
            {
                context.Metadata["PipelineError"] = ex;
                throw;
            }
        }
    }
}

using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Provides the typed Play Mode entry application handler. </summary>
internal sealed class PlayEnterService : IPlayEnterService
{
    private readonly PlayTransitionWorkflow workflow;

    public PlayEnterService (PlayTransitionWorkflow workflow)
    {
        this.workflow = workflow
            ?? throw new ArgumentNullException(nameof(workflow));
    }

    /// <inheritdoc />
    public async ValueTask<PlayEnterExecutionResult> StartAsync (
        LifecycleExecutionStartInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        return CreateResult(
            await workflow.StartAsync(
                    invocation,
                    PlayEnterTransitionDirectionPolicy.Instance,
                    cancellationToken)
                .ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<PlayEnterExecutionResult> ReconnectAsync (
        LifecycleExecutionReconnectInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        return CreateResult(
            await workflow.ReconnectAsync(
                    invocation,
                    PlayEnterTransitionDirectionPolicy.Instance,
                    cancellationToken)
                .ConfigureAwait(false));
    }

    private static PlayEnterExecutionResult CreateResult (
        PlayTransitionWorkflowResult<PlayEnterExecutionOutput> result)
    {
        return result.IsSuccess
            ? PlayEnterExecutionResult.Success(result.Output!)
            : PlayEnterExecutionResult.Failure(
                result.Error!,
                result.FailureContext);
    }
}

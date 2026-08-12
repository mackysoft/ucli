using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Exit;

/// <summary> Provides the typed Play Mode exit application handler. </summary>
internal sealed class PlayExitService : IPlayExitService
{
    private readonly PlayTransitionWorkflow workflow;

    public PlayExitService (PlayTransitionWorkflow workflow)
    {
        this.workflow = workflow
            ?? throw new ArgumentNullException(nameof(workflow));
    }

    /// <inheritdoc />
    public async ValueTask<PlayExitExecutionResult> StartAsync (
        LifecycleExecutionStartInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        return CreateResult(
            await workflow.StartAsync(
                    invocation,
                    PlayExitTransitionDirectionPolicy.Instance,
                    cancellationToken)
                .ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<PlayExitExecutionResult> ReconnectAsync (
        LifecycleExecutionReconnectInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        return CreateResult(
            await workflow.ReconnectAsync(
                    invocation,
                    PlayExitTransitionDirectionPolicy.Instance,
                    cancellationToken)
                .ConfigureAwait(false));
    }

    private static PlayExitExecutionResult CreateResult (
        PlayTransitionWorkflowResult<PlayExitExecutionOutput> result)
    {
        return result.IsSuccess
            ? PlayExitExecutionResult.Success(result.Output!)
            : PlayExitExecutionResult.Failure(
                result.Error!,
                result.FailureContext);
    }
}

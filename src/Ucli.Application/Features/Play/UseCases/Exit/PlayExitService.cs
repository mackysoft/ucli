using MackySoft.Ucli.Application.Features.Play.Common;

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
    public async ValueTask<PlayExitExecutionResult> ExecuteAsync (
        PlayExitCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return CreateResult(
            await workflow.ExecuteAsync(
                    input.ProjectPath,
                    input.TimeoutMilliseconds,
                    PlayExitTransitionDirectionPolicy.Instance,
                    cancellationToken)
                .ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<PlayExitExecutionResult> ReconnectAsync (
        PlayExitCommandInput input,
        ExecutionRef lifecycleExecutionRef,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(lifecycleExecutionRef);
        return CreateResult(
            await workflow.ReconnectAsync(
                    input.ProjectPath,
                    input.TimeoutMilliseconds,
                    lifecycleExecutionRef,
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

using MackySoft.Ucli.Application.Features.Play.Common;

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
    public async ValueTask<PlayEnterExecutionResult> ExecuteAsync (
        PlayEnterCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return CreateResult(
            await workflow.ExecuteAsync(
                    input.ProjectPath,
                    input.TimeoutMilliseconds,
                    PlayEnterTransitionDirectionPolicy.Instance,
                    cancellationToken)
                .ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async ValueTask<PlayEnterExecutionResult> ReconnectAsync (
        PlayEnterCommandInput input,
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

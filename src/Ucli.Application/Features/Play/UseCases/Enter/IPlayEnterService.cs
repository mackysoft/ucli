namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Provides Play Mode enter workflow execution. </summary>
internal interface IPlayEnterService
{
    /// <summary> Executes one Play Mode enter workflow. </summary>
    /// <param name="input"> The normalized Play Mode transition input. </param>
    /// <param name="cancellationToken"> The application caller's wait cancellation token. </param>
    /// <returns> A task that resolves to the Play Mode enter result. </returns>
    ValueTask<PlayEnterExecutionResult> ExecuteAsync (
        PlayEnterCommandInput input,
        CancellationToken cancellationToken = default);

    /// <summary> Reconnects to one previously published Play Mode enter Lifecycle Execution. </summary>
    /// <param name="input"> The normalized caller wait input. </param>
    /// <param name="lifecycleExecutionRef"> The published Play Mode enter execution reference. </param>
    /// <param name="cancellationToken"> The application caller's wait cancellation token. </param>
    /// <returns> A task that resolves to the result collected from the original transition. </returns>
    ValueTask<PlayEnterExecutionResult> ReconnectAsync (
        PlayEnterCommandInput input,
        ExecutionRef lifecycleExecutionRef,
        CancellationToken cancellationToken = default);
}

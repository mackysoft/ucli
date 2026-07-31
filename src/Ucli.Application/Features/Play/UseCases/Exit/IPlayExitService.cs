namespace MackySoft.Ucli.Application.Features.Play.UseCases.Exit;

/// <summary> Provides Play Mode exit workflow execution. </summary>
internal interface IPlayExitService
{
    /// <summary> Executes one Play Mode exit workflow. </summary>
    /// <param name="input"> The normalized Play Mode transition input. </param>
    /// <param name="cancellationToken"> The application caller's wait cancellation token. </param>
    /// <returns> A task that resolves to the Play Mode exit result. </returns>
    ValueTask<PlayExitExecutionResult> ExecuteAsync (
        PlayExitCommandInput input,
        CancellationToken cancellationToken = default);

    /// <summary> Reconnects to one previously published Play Mode exit Lifecycle Execution. </summary>
    /// <param name="input"> The normalized caller wait input. </param>
    /// <param name="lifecycleExecutionRef"> The published Play Mode exit execution reference. </param>
    /// <param name="cancellationToken"> The application caller's wait cancellation token. </param>
    /// <returns> A task that resolves to the result collected from the original transition. </returns>
    ValueTask<PlayExitExecutionResult> ReconnectAsync (
        PlayExitCommandInput input,
        ExecutionRef lifecycleExecutionRef,
        CancellationToken cancellationToken = default);
}

namespace MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;

/// <summary> Executes the typed project-refresh application workflow. </summary>
internal interface IRefreshService
{
    /// <summary> Executes one project refresh and returns the normalized execution result. </summary>
    /// <param name="requestId"> The non-empty correlation identifier owned by the application caller. </param>
    /// <param name="input"> The normalized command input values. </param>
    /// <param name="cancellationToken"> The application caller's wait cancellation token. </param>
    /// <returns> A task that resolves to the refresh execution result. </returns>
    ValueTask<RefreshExecutionResult> ExecuteAsync (
        Guid requestId,
        RefreshCommandInput input,
        CancellationToken cancellationToken = default);

    /// <summary> Reconnects to one previously published project-refresh Lifecycle Execution. </summary>
    /// <param name="requestId"> The non-empty correlation identifier owned by the current caller. </param>
    /// <param name="input"> The normalized caller wait and execution-target input. </param>
    /// <param name="lifecycleExecutionRef"> The published refresh execution reference to reconnect. </param>
    /// <param name="cancellationToken"> The application caller's wait cancellation token. </param>
    /// <returns> A task that resolves to the result collected from the original refresh execution. </returns>
    ValueTask<RefreshExecutionResult> ReconnectAsync (
        Guid requestId,
        RefreshCommandInput input,
        ExecutionRef lifecycleExecutionRef,
        CancellationToken cancellationToken = default);
}

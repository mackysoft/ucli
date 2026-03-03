namespace MackySoft.Ucli.Status;

/// <summary> Executes the status command workflow and produces one normalized status result. </summary>
internal interface IStatusService
{
    /// <summary> Executes status workflow using project, timeout, and daemon diagnostics contracts. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the status execution result. </returns>
    ValueTask<StatusExecutionResult> Execute (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default);
}
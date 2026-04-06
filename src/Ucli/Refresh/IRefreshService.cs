using MackySoft.Ucli.Execution.OperationExecute;

namespace MackySoft.Ucli.Refresh;

/// <summary> Executes the <c>refresh</c> command workflow. </summary>
internal interface IRefreshService
{
    /// <summary> Executes one <c>refresh</c> workflow and returns the normalized execution result. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. </param>
    /// <param name="mode"> The optional <c>--mode</c> value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> value in milliseconds. </param>
    /// <param name="waitUntilReady"> Whether Unity-side execution may wait for lifecycle readiness before failing. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the refresh execution result. </returns>
    ValueTask<OperationExecuteResult> Execute (
        string? projectPath,
        string? mode,
        string? timeout,
        bool waitUntilReady,
        CancellationToken cancellationToken = default);
}
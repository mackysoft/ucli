namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Executes daemon-list workflow and returns normalized command output values. </summary>
internal interface IDaemonListCommandService
{
    /// <summary> Executes one daemon-list workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-list execution result. </returns>
    ValueTask<DaemonListExecutionResult> GetList (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default);
}

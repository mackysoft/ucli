namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Executes daemon-status workflow and returns normalized command output values. </summary>
internal interface IDaemonStatusCommandService
{
    /// <summary> Executes one daemon-status workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-status execution result. </returns>
    ValueTask<DaemonStatusExecutionResult> GetStatus (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default);
}
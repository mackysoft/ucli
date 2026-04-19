namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Executes daemon-start workflow and returns normalized command output values. </summary>
internal interface IDaemonStartService
{
    /// <summary> Executes one daemon-start workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-start execution result. </returns>
    ValueTask<DaemonStartExecutionResult> Start (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default);
}
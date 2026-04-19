namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Executes daemon-stop workflow and returns normalized command output values. </summary>
internal interface IDaemonStopService
{
    /// <summary> Executes one daemon-stop workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-stop execution result. </returns>
    ValueTask<DaemonStopExecutionResult> Stop (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default);
}
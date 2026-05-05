namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;

/// <summary> Executes daemon-stop workflow and returns normalized command output values. </summary>
internal interface IDaemonStopService
{
    /// <summary> Executes one daemon-stop workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeoutMilliseconds"> The optional normalized timeout value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-stop execution result. </returns>
    ValueTask<DaemonStopExecutionResult> Stop (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default);
}

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;

/// <summary> Executes daemon-status workflow and returns normalized command output values. </summary>
internal interface IDaemonStatusService
{
    /// <summary> Executes one daemon-status workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeoutMilliseconds"> The optional normalized timeout value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-status execution result. </returns>
    ValueTask<DaemonStatusExecutionResult> GetStatusAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default);
}

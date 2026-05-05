namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;

/// <summary> Executes daemon-cleanup workflow and returns normalized command output values. </summary>
internal interface IDaemonCleanupService
{
    /// <summary> Executes one daemon-cleanup workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeoutMilliseconds"> The optional normalized timeout value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-cleanup execution result. </returns>
    ValueTask<DaemonCleanupExecutionResult> Cleanup (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default);
}

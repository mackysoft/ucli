namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;

/// <summary> Executes daemon-list workflow and returns normalized command output values. </summary>
internal interface IDaemonListService
{
    /// <summary> Executes one daemon-list workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeoutMilliseconds"> The optional normalized timeout value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-list execution result. </returns>
    ValueTask<DaemonListExecutionResult> GetListAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default);
}

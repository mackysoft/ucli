namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

/// <summary> Orchestrates <c>logs unity clear</c> command execution. </summary>
internal interface ILogsUnityClearService
{
    /// <summary> Clears the Unity Editor Console display through the daemon IPC session. </summary>
    /// <param name="request"> The command request values. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The clear execution result. </returns>
    ValueTask<LogsUnityClearServiceResult> ExecuteAsync (
        LogsUnityClearServiceRequest request,
        CancellationToken cancellationToken = default);
}

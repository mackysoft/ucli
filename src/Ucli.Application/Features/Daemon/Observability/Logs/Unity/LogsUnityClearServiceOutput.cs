namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

/// <summary> Represents successful <c>logs unity clear</c> command output. </summary>
/// <param name="ClearStatus"> The Unity Console clear status. </param>
/// <param name="TimeoutMilliseconds"> The effective IPC timeout in milliseconds. </param>
internal sealed record LogsUnityClearServiceOutput (
    LogsUnityClearStatus ClearStatus,
    int TimeoutMilliseconds);

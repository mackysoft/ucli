using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Logs;

/// <summary> Provides orchestration for <c>logs unity</c> command workflows. </summary>
internal interface ILogsUnityService
{
    /// <summary> Executes one Unity-log workflow and emits events through callback delegate. </summary>
    /// <param name="request"> The normalized command option values. </param>
    /// <param name="onEvent"> The event callback invoked for each emitted event. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Unity-log service execution result. </returns>
    ValueTask<LogsDaemonServiceResult> Execute (
        LogsUnityServiceRequest request,
        Func<IpcUnityLogEvent, string, CancellationToken, ValueTask> onEvent,
        CancellationToken cancellationToken = default);
}
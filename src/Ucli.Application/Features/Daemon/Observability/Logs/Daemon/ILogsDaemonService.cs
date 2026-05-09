using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;

/// <summary> Provides orchestration for <c>logs daemon read</c> command workflows. </summary>
internal interface ILogsDaemonService
{
    /// <summary> Executes one daemon-log workflow and emits events through callback delegate. </summary>
    /// <param name="request"> The normalized command option values. </param>
    /// <param name="onEvent"> The event callback invoked for each emitted event. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-log service execution result. </returns>
    ValueTask<LogsReadServiceResult> ExecuteAsync (
        LogsDaemonServiceRequest request,
        Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask> onEvent,
        CancellationToken cancellationToken = default);
}

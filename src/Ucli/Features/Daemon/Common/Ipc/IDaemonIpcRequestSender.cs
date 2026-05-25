using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Common.Ipc;

/// <summary> Sends daemon IPC requests through the persisted session endpoint with recovery-aware retry handling. </summary>
internal interface IDaemonIpcRequestSender
{
    /// <summary> Sends one single-response daemon IPC request. </summary>
    ValueTask<DaemonIpcSendResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        Func<string, IpcRequest> createRequest,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

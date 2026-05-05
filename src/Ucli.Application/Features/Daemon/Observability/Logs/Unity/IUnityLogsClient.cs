using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

/// <summary> Sends Unity-log read queries over Unity IPC transport. </summary>
internal interface IUnityLogsClient
{
    /// <summary> Sends one Unity-log read query and returns decoded IPC payload or structured error. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="query"> The Unity-log query values. </param>
    /// <param name="timeout"> The IPC timeout used by the request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
    /// <returns> The Unity-log read attempt result. </returns>
    ValueTask<UnityLogsClientReadResult> Read (
        ResolvedUnityProjectContext unityProject,
        IpcUnityLogsReadRequest query,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

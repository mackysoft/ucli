using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Shared.Context.Project;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;

/// <summary> Sends daemon-log read queries over Unity IPC transport. </summary>
internal interface IDaemonLogsClient
{
    /// <summary> Sends one daemon-log read query and returns decoded IPC payload or structured error. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="query"> The daemon-log query values. </param>
    /// <param name="timeout"> The IPC timeout used by the request. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
    /// <returns> The daemon-log read attempt result. </returns>
    ValueTask<DaemonLogsClientReadResult> Read (
        ResolvedUnityProjectContext unityProject,
        IpcDaemonLogsReadRequest query,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

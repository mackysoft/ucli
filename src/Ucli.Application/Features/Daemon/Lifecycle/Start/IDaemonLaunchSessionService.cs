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
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Shared.Context.Project;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Persists launch-session snapshots required by daemon startup workflow. </summary>
internal interface IDaemonLaunchSessionService
{
    /// <summary> Creates and persists an initial daemon session before process launch. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The launch-session persistence result. </returns>
    ValueTask<DaemonLaunchSessionWriteResult> Initialize (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);

    /// <summary> Persists launched daemon process identifier to an existing session snapshot. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session snapshot. </param>
    /// <param name="processId"> The launched process identifier when available. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The launch-session persistence result. </returns>
    ValueTask<DaemonLaunchSessionWriteResult> UpdateProcessId (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        int? processId,
        CancellationToken cancellationToken = default);
}

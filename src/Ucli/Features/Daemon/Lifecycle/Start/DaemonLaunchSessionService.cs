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
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start;

/// <summary> Implements launch-session creation and persistence for daemon startup workflow. </summary>
internal sealed class DaemonLaunchSessionService : IDaemonLaunchSessionService
{
    private readonly IIpcEndpointResolver endpointResolver;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonSessionTokenGenerator sessionTokenGenerator;

    /// <summary> Initializes a new instance of the <see cref="DaemonLaunchSessionService" /> class. </summary>
    /// <param name="endpointResolver"> The IPC endpoint resolver dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session-store dependency. </param>
    /// <param name="sessionTokenGenerator"> The daemon session-token generator dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonLaunchSessionService (
        IIpcEndpointResolver endpointResolver,
        IDaemonSessionStore daemonSessionStore,
        IDaemonSessionTokenGenerator sessionTokenGenerator)
    {
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.sessionTokenGenerator = sessionTokenGenerator ?? throw new ArgumentNullException(nameof(sessionTokenGenerator));
    }

    /// <summary> Creates and persists an initial daemon session before process launch. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The launch-session persistence result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonLaunchSessionWriteResult> Initialize (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var endpoint = endpointResolver.Resolve(unityProject.RepositoryRoot, unityProject.ProjectFingerprint);
        var session = new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: sessionTokenGenerator.Create(),
            ProjectFingerprint: unityProject.ProjectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: IpcTransportKindCodec.ToValue(endpoint.TransportKind),
            EndpointAddress: endpoint.Address,
            ProcessId: null,
            OwnerProcessId: Environment.ProcessId);

        var writeResult = await daemonSessionStore.Write(
                unityProject.RepositoryRoot,
                session,
                cancellationToken)
            .ConfigureAwait(false);
        if (!writeResult.IsSuccess)
        {
            return DaemonLaunchSessionWriteResult.Failure(writeResult.Error!);
        }

        return DaemonLaunchSessionWriteResult.Success(session);
    }

    /// <summary> Persists launched daemon process identifier to an existing session snapshot. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session snapshot. </param>
    /// <param name="processId"> The launched process identifier when available. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The launch-session persistence result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="session" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonLaunchSessionWriteResult> UpdateProcessId (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        int? processId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);

        if (processId is not int launchedProcessId)
        {
            return DaemonLaunchSessionWriteResult.Success(session);
        }

        var updatedSession = session with { ProcessId = launchedProcessId };
        var writeResult = await daemonSessionStore.Write(
                unityProject.RepositoryRoot,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        if (!writeResult.IsSuccess)
        {
            return DaemonLaunchSessionWriteResult.Failure(writeResult.Error!);
        }

        return DaemonLaunchSessionWriteResult.Success(updatedSession);
    }
}

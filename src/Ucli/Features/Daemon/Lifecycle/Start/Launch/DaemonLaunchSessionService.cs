using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Launch;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start.Launch;

/// <summary> Implements launch-session creation and persistence for daemon startup workflow. </summary>
internal sealed class DaemonLaunchSessionService : IDaemonLaunchSessionService
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonSessionTokenGenerator sessionTokenGenerator;

    private readonly IGuidGenerator sessionGenerationIdGenerator;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonLaunchSessionService" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session-store dependency. </param>
    /// <param name="sessionTokenGenerator"> The daemon session-token generator dependency. </param>
    /// <param name="sessionGenerationIdGenerator"> The source of non-empty identities for persisted daemon session generations. </param>
    /// <param name="timeProvider"> The time source for persisted session issue timestamps. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonLaunchSessionService (
        IDaemonSessionStore daemonSessionStore,
        IDaemonSessionTokenGenerator sessionTokenGenerator,
        IGuidGenerator sessionGenerationIdGenerator,
        TimeProvider timeProvider)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.sessionTokenGenerator = sessionTokenGenerator ?? throw new ArgumentNullException(nameof(sessionTokenGenerator));
        this.sessionGenerationIdGenerator = sessionGenerationIdGenerator ?? throw new ArgumentNullException(nameof(sessionGenerationIdGenerator));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Creates and persists an initial daemon session before process launch. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="editorMode"> The requested daemon Editor mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The launch-session persistence result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonLaunchSessionWriteResult> InitializeAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonEditorMode editorMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        if (!DaemonLaunchEditorModePolicy.TryResolve(editorMode, out var launchEditorMode, out var editorModeError))
        {
            return DaemonLaunchSessionWriteResult.Failure(editorModeError!);
        }

        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(unityProject.RepositoryRoot, unityProject.ProjectFingerprint);
        var session = new DaemonSession(
            sessionGenerationIdGenerator.Generate(),
            sessionTokenGenerator.Create(),
            unityProject.ProjectFingerprint,
            timeProvider.GetUtcNow(),
            launchEditorMode,
            DaemonSessionOwnerKind.Cli,
            canShutdownProcess: true,
            endpoint.Contract,
            endpoint.UnixSocketPath,
            processId: null,
            processStartedAtUtc: null,
            Environment.ProcessId,
            editorInstanceId: null);
        DaemonSessionIpcTransportEndpointAdapter.Bind(session, endpoint);

        var writeResult = await daemonSessionStore.WriteAsync(
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

    /// <summary> Persists launched daemon process identity to an existing session snapshot. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session snapshot. </param>
    /// <param name="processId"> The launched process identifier when available. </param>
    /// <param name="processStartedAtUtc"> The launched process start timestamp when available. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The launch-session persistence result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="session" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonLaunchSessionWriteResult> UpdateProcessIdAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);

        if (processId is not int launchedProcessId)
        {
            return DaemonLaunchSessionWriteResult.Success(session);
        }

        if (processStartedAtUtc is null || processStartedAtUtc.Value == default)
        {
            return DaemonLaunchSessionWriteResult.Failure(ExecutionError.InternalError(
                $"Daemon launch processStartedAtUtc is required when processId is specified. processId={launchedProcessId}."));
        }

        var updatedSession = new DaemonSession(
            session.SessionGenerationId,
            session.SessionToken,
            session.ProjectFingerprint,
            session.IssuedAtUtc,
            session.EditorMode,
            session.OwnerKind,
            session.CanShutdownProcess,
            session.EndpointContract,
            session.UnixSocketEndpointPath,
            launchedProcessId,
            processStartedAtUtc.Value,
            session.OwnerProcessId,
            session.EditorInstanceId);
        DaemonSessionIpcTransportEndpointAdapter.Bind(
            updatedSession,
            DaemonSessionIpcTransportEndpointAdapter.Adapt(session));
        var writeResult = await daemonSessionStore.WriteAsync(
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

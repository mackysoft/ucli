using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingDaemonLaunchSessionService : IDaemonLaunchSessionService
{
    private readonly List<InitializeInvocation> initializeInvocations = [];

    private readonly List<UpdateProcessIdInvocation> updateProcessIdInvocations = [];

    public DaemonLaunchSessionWriteResult InitializeResult { get; set; } = DaemonLaunchSessionWriteResult.Success(DaemonSessionTestFactory.Create(
        processId: null,
        sessionToken: "session-token",
        endpointAddress: "ucli-daemon-test-endpoint"));

    public DaemonLaunchSessionWriteResult? UpdateProcessIdResult { get; set; }

    public Func<ResolvedUnityProjectContext, DaemonEditorMode, CancellationToken, ValueTask<DaemonLaunchSessionWriteResult>>? InitializeHandler { get; set; }

    public Func<ResolvedUnityProjectContext, DaemonSession, int?, DateTimeOffset?, CancellationToken, ValueTask<DaemonLaunchSessionWriteResult>>? UpdateProcessIdHandler { get; set; }

    public IReadOnlyList<InitializeInvocation> InitializeInvocations => initializeInvocations;

    public IReadOnlyList<UpdateProcessIdInvocation> UpdateProcessIdInvocations => updateProcessIdInvocations;

    public ValueTask<DaemonLaunchSessionWriteResult> InitializeAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonEditorMode editorMode,
        CancellationToken cancellationToken = default)
    {
        initializeInvocations.Add(new InitializeInvocation(unityProject, editorMode, cancellationToken));
        if (InitializeHandler is not null)
        {
            return InitializeHandler(unityProject, editorMode, cancellationToken);
        }

        return ValueTask.FromResult(InitializeResult);
    }

    public ValueTask<DaemonLaunchSessionWriteResult> UpdateProcessIdAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        CancellationToken cancellationToken = default)
    {
        updateProcessIdInvocations.Add(new UpdateProcessIdInvocation(unityProject, session, processId, processStartedAtUtc, cancellationToken));
        if (UpdateProcessIdHandler is not null)
        {
            return UpdateProcessIdHandler(
                unityProject,
                session,
                processId,
                processStartedAtUtc,
                cancellationToken);
        }

        if (UpdateProcessIdResult is not null)
        {
            return ValueTask.FromResult(UpdateProcessIdResult);
        }

        var updatedSession = processId is int pid
            ? new DaemonSession(
                session.SessionGenerationId,
                session.SessionToken,
                session.ProjectFingerprint,
                session.IssuedAtUtc,
                session.EditorMode,
                session.OwnerKind,
                session.CanShutdownProcess,
                session.EndpointContract,
                session.UnixSocketEndpointPath,
                pid,
                processStartedAtUtc ?? throw new ArgumentNullException(nameof(processStartedAtUtc)),
                session.OwnerProcessId,
                session.EditorInstanceId)
            : session;
        return ValueTask.FromResult(DaemonLaunchSessionWriteResult.Success(updatedSession));
    }

    internal readonly record struct InitializeInvocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonEditorMode EditorMode,
        CancellationToken CancellationToken);

    internal readonly record struct UpdateProcessIdInvocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSession Session,
        int? ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        CancellationToken CancellationToken);
}

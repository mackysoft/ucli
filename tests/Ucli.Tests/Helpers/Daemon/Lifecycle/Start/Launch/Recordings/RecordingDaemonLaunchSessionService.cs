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

    public IReadOnlyList<InitializeInvocation> InitializeInvocations => initializeInvocations;

    public IReadOnlyList<UpdateProcessIdInvocation> UpdateProcessIdInvocations => updateProcessIdInvocations;

    public ValueTask<DaemonLaunchSessionWriteResult> InitializeAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonEditorMode editorMode,
        CancellationToken cancellationToken = default)
    {
        initializeInvocations.Add(new InitializeInvocation(unityProject, editorMode, cancellationToken));
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
        if (UpdateProcessIdResult is not null)
        {
            return ValueTask.FromResult(UpdateProcessIdResult);
        }

        var updatedSession = processId is int pid
            ? session with { ProcessId = pid, ProcessStartedAtUtc = processStartedAtUtc }
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

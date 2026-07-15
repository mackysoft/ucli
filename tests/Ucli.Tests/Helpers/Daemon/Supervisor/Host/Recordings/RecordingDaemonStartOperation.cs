using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingDaemonStartOperation : IDaemonStartOperation
{
    private readonly List<Invocation> invocations = [];

    public DaemonStartResult StartResult { get; set; } = CreateDefaultStartResult();

    public TimeSpan DelayBeforeResult { get; set; }

    public bool WaitUntilCancellation { get; set; }

    public Func<IDaemonStartProgressObserver?, CancellationToken, ValueTask>? OnStart { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public async ValueTask<DaemonStartResult> StartAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonStartProgressObserver? progressObserver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.TryGetRemainingTimeout(out var remainingTimeout);

        invocations.Add(new Invocation(
            unityProject,
            deadline,
            remainingTimeout,
            editorMode,
            onStartupBlocked,
            progressObserver,
            cancellationToken));
        if (OnStart is not null)
        {
            await OnStart(progressObserver, cancellationToken).ConfigureAwait(false);
        }

        if (WaitUntilCancellation)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }

        if (DelayBeforeResult > TimeSpan.Zero)
        {
            await Task.Delay(DelayBeforeResult, cancellationToken).ConfigureAwait(false);
        }

        return StartResult;
    }

    private static DaemonStartResult CreateDefaultStartResult ()
    {
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
            issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            editorMode: DaemonEditorMode.Batchmode,
            ownerKind: DaemonSessionOwnerKind.Cli,
            canShutdownProcess: true,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock",
            processId: 42,
            processStartedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 1, TimeSpan.Zero),
            ownerProcessId: 24);
        return DaemonStartResult.AlreadyRunning(
            session,
            IpcUnityEditorObservationTestFactory.Create(projectFingerprint: session.ProjectFingerprint));
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        ExecutionDeadline Deadline,
        TimeSpan RemainingTimeout,
        DaemonEditorMode? EditorMode,
        DaemonStartupBlockedProcessPolicy OnStartupBlocked,
        IDaemonStartProgressObserver? ProgressObserver,
        CancellationToken CancellationToken);
}

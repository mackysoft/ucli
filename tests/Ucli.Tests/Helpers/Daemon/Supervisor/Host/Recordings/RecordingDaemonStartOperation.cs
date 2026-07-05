using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingDaemonStartOperation : IDaemonStartOperation
{
    private readonly List<Invocation> invocations = [];

    public DaemonStartResult StartResult { get; set; } = DaemonStartResult.AlreadyRunning(CreateDefaultSession());

    public TimeSpan DelayBeforeResult { get; set; }

    public bool WaitUntilCancellation { get; set; }

    public Func<IDaemonStartProgressObserver?, CancellationToken, ValueTask>? OnStart { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public async ValueTask<DaemonStartResult> StartAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonStartProgressObserver? progressObserver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(
            unityProject,
            timeout,
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

    private static DaemonSession CreateDefaultSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            EditorMode: "batchmode",
            OwnerKind: "cli",
            CanShutdownProcess: true,
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli.sock",
            ProcessId: 42,
            ProcessStartedAtUtc: null,
            OwnerProcessId: 24);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        DaemonEditorMode? EditorMode,
        DaemonStartupBlockedProcessPolicy OnStartupBlocked,
        IDaemonStartProgressObserver? ProgressObserver,
        CancellationToken CancellationToken);
}

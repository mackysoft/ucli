using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonGuiSessionRegistrationAwaiter : IDaemonGuiSessionRegistrationAwaiter
{
    private readonly List<Invocation> invocations = [];
    private Action<int>? onWait;

    public DaemonGuiSessionRegistrationWaitResult Result { get; set; } =
        DaemonGuiSessionRegistrationWaitResult.Success(CreateDefaultSession(), IpcUnityEditorObservationTestFactory.Create());

    public DaemonGuiSessionRegistrationWaitResult NextResult
    {
        get => Result;
        set => Result = value;
    }

    public Queue<DaemonGuiSessionRegistrationWaitResult> Results { get; } = [];

    public Action? OnWaitForSession { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public void AdvanceTimeOnFirstWait (
        ManualTimeProvider timeProvider,
        TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        onWait = waitNumber =>
        {
            if (waitNumber == 1)
            {
                timeProvider.Advance(elapsed);
            }
        };
    }

    public ValueTask<DaemonGuiSessionRegistrationWaitResult> WaitForSessionAsync (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        TimeSpan timeout,
        DateTimeOffset? expectedProcessStartedAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(unityProject, expectedProcessId, timeout, expectedProcessStartedAtUtc, cancellationToken));
        OnWaitForSession?.Invoke();
        onWait?.Invoke(invocations.Count);
        return ValueTask.FromResult(Results.Count > 0 ? Results.Dequeue() : Result);
    }

    private static DaemonSession CreateDefaultSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "secret-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 05, 0, 0, 0, TimeSpan.Zero),
            EditorMode: DaemonEditorMode.Batchmode,
            OwnerKind: DaemonSessionOwnerKind.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: IpcTransportKind.NamedPipe,
            EndpointAddress: "ucli-daemon-endpoint",
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 9876);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        int ExpectedProcessId,
        TimeSpan Timeout,
        DateTimeOffset? ExpectedProcessStartedAtUtc,
        CancellationToken CancellationToken);
}

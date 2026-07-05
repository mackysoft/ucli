namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonGuiRebootstrapClient : IDaemonGuiRebootstrapClient
{
    private readonly List<Invocation> invocations = [];

    public DaemonGuiRebootstrapRequestResult Result { get; set; } =
        DaemonGuiRebootstrapRequestResult.Accepted();

    public Action? OnRequest { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonGuiRebootstrapRequestResult> RequestRebootstrapAsync (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        DateTimeOffset? expectedProcessStartedAtUtc,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(unityProject, expectedProcessId, expectedProcessStartedAtUtc, timeout, cancellationToken));
        OnRequest?.Invoke();
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        int ExpectedProcessId,
        DateTimeOffset? ExpectedProcessStartedAtUtc,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}

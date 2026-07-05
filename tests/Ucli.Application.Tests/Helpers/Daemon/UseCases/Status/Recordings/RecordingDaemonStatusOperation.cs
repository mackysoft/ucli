using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonStatusOperation : IDaemonStatusOperation
{
    private readonly DaemonStatusResult result;
    private readonly List<Invocation> invocations = [];

    public RecordingDaemonStatusOperation (DaemonStatusResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public Action? OnGetStatus { get; set; }

    public ValueTask<DaemonStatusResult> GetStatusAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(
            unityProject,
            timeout,
            cancellationToken));
        OnGetStatus?.Invoke();

        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}

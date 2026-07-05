using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingDaemonLaunchCompensationService : IDaemonLaunchCompensationService
{
    private readonly List<Invocation> invocations = [];

    public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

    public Action? OnCleanup { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonSessionStoreOperationResult> CleanupFailedLaunchAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonProcessTerminationTarget? target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(unityProject, target, timeout, cancellationToken));
        OnCleanup?.Invoke();
        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonProcessTerminationTarget? Target,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}

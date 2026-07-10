using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingDaemonLaunchCompensationService : IDaemonLaunchCompensationService
{
    private readonly List<Invocation> invocations = [];

    public DaemonSessionStoreOperationResult NextResult { get; set; } = DaemonSessionStoreOperationResult.Success();

    public Action? OnCleanup { get; set; }

    public Func<ResolvedUnityProjectContext, DaemonSession?, DaemonProcessTerminationTarget?, TimeSpan, CancellationToken, ValueTask<DaemonSessionStoreOperationResult>>? Handler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonSessionStoreOperationResult> CleanupFailedLaunchAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession? expectedSession,
        DaemonProcessTerminationTarget? target,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        invocations.Add(new Invocation(unityProject, expectedSession, target, timeout, cancellationToken));
        OnCleanup?.Invoke();
        if (Handler is not null)
        {
            return Handler(unityProject, expectedSession, target, timeout, cancellationToken);
        }

        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSession? ExpectedSession,
        DaemonProcessTerminationTarget? Target,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}

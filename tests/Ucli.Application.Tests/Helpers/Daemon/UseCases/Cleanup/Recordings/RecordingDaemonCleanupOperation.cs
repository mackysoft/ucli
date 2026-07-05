using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonCleanupOperation : IDaemonCleanupOperation
{
    private readonly DaemonCleanupResult result;
    private readonly List<Invocation> invocations = [];

    public RecordingDaemonCleanupOperation (DaemonCleanupResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonCleanupResult> CleanupAsync (
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

        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}

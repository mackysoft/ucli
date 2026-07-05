using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed class QueuedDaemonSessionConnectionProvider : IDaemonSessionConnectionProvider
{
    private readonly Queue<DaemonSessionConnectionResolutionResult> results;

    private DaemonSessionConnectionResolutionResult lastResult;

    public QueuedDaemonSessionConnectionProvider (params DaemonSessionConnectionResolutionResult[] results)
    {
        this.results = new Queue<DaemonSessionConnectionResolutionResult>(results ?? throw new ArgumentNullException(nameof(results)));
        lastResult = results.Length == 0
            ? DaemonSessionConnectionResolutionResult.SessionNotAvailable()
            : results[^1];
    }

    public ValueTask<DaemonSessionConnectionResolutionResult> ResolveAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();
        if (results.Count != 0)
        {
            lastResult = results.Dequeue();
        }

        return ValueTask.FromResult(lastResult);
    }
}

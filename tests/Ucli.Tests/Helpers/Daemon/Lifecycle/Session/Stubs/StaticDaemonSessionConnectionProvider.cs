using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed class StaticDaemonSessionConnectionProvider : IDaemonSessionConnectionProvider
{
    private readonly DaemonSessionConnectionResolutionResult result;

    public StaticDaemonSessionConnectionProvider (DaemonSessionConnectionResolutionResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public ValueTask<DaemonSessionConnectionResolutionResult> ResolveAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(result);
    }
}

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class UnexpectedDaemonArtifactCleaner : IDaemonArtifactCleaner
{
    private readonly string reason;

    public UnexpectedDaemonArtifactCleaner (string reason)
    {
        this.reason = reason;
    }

    public ValueTask<DaemonArtifactCleanupResult> CleanupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }
}

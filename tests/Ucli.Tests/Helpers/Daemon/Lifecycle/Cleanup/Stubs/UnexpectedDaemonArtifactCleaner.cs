using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class UnexpectedDaemonArtifactCleaner : IDaemonArtifactCleaner
{
    private readonly string reason;

    public UnexpectedDaemonArtifactCleaner (string reason)
    {
        this.reason = reason;
    }

    public ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionMissingAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession expectedSession,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask<DaemonArtifactCleanupResult> CleanupIfStoppedProcessMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonProcessTerminationTarget stoppedProcess,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionArtifactMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionArtifactIdentity expectedArtifactIdentity,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }
}

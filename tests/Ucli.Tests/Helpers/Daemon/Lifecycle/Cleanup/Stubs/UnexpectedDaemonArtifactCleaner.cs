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
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession expectedSession,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask<DaemonArtifactCleanupResult> CleanupIfStoppedProcessMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonProcessTerminationTarget stoppedProcess,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }

    public ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionArtifactMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionArtifactIdentity expectedArtifactIdentity,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(reason);
    }
}

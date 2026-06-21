using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Implements stale daemon artifact cleanup for one project fingerprint. </summary>
internal sealed class DaemonArtifactCleaner : IDaemonArtifactCleaner
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonLifecycleStore daemonLifecycleStore;

    private readonly IDaemonLaunchAttemptStore launchAttemptStore;

    /// <summary> Initializes a new instance of the <see cref="DaemonArtifactCleaner" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonArtifactCleaner (
        IDaemonSessionStore daemonSessionStore,
        IDaemonLifecycleStore daemonLifecycleStore,
        IDaemonLaunchAttemptStore launchAttemptStore)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonLifecycleStore = daemonLifecycleStore ?? throw new ArgumentNullException(nameof(daemonLifecycleStore));
        this.launchAttemptStore = launchAttemptStore ?? throw new ArgumentNullException(nameof(launchAttemptStore));
    }

    /// <summary> Cleans stale daemon artifacts for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup operation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonArtifactCleanupResult> CleanupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var deleteSessionResult = await daemonSessionStore.DeleteAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!deleteSessionResult.IsSuccess)
        {
            return DaemonArtifactCleanupResult.Failure(deleteSessionResult.Error!);
        }

        var deleteLifecycleResult = await daemonLifecycleStore.DeleteAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!deleteLifecycleResult.IsSuccess)
        {
            return DaemonArtifactCleanupResult.Failure(deleteLifecycleResult.Error!);
        }

        var pruneResult = await launchAttemptStore.PruneAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                keepCount: 20,
                cancellationToken)
            .ConfigureAwait(false);
        if (!pruneResult.IsSuccess)
        {
            return DaemonArtifactCleanupResult.Failure(pruneResult.Error!);
        }

        try
        {
            var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint);
            if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
            {
                FileUtilities.DeleteIfExists(endpoint.Address);

                UnixSocketPathUtilities.DeleteEmptyFallbackDirectoryIfPresent(
                    endpoint.Address,
                    UcliIpcEndpointNames.DaemonAddressPrefix);
            }

            return DaemonArtifactCleanupResult.Success(pruneResult.DeletedCount);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DaemonArtifactCleanupResult.Failure(ExecutionError.InternalError(
                $"Failed to cleanup daemon endpoint residue. {exception.Message}"));
        }
    }
}

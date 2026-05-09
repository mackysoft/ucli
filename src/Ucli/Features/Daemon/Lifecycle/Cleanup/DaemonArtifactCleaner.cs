using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Implements stale daemon artifact cleanup for one project fingerprint. </summary>
internal sealed class DaemonArtifactCleaner : IDaemonArtifactCleaner
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IIpcEndpointResolver endpointResolver;

    /// <summary> Initializes a new instance of the <see cref="DaemonArtifactCleaner" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="endpointResolver"> The endpoint resolver dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonArtifactCleaner (
        IDaemonSessionStore daemonSessionStore,
        IIpcEndpointResolver endpointResolver)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
    }

    /// <summary> Cleans stale daemon artifacts for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup operation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonSessionStoreOperationResult> CleanupAsync (
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
            return deleteSessionResult;
        }

        try
        {
            var endpoint = endpointResolver.Resolve(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint);
            if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
            {
                FileUtilities.DeleteIfExists(endpoint.Address);

                UnixSocketPathUtilities.DeleteEmptyFallbackDirectoryIfPresent(
                    endpoint.Address,
                    UcliIpcEndpointNames.DaemonAddressPrefix);
            }

            return DaemonSessionStoreOperationResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to cleanup daemon endpoint residue. {exception.Message}"));
        }
    }
}

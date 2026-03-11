using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

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
    public async ValueTask<DaemonSessionStoreOperationResult> Cleanup (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var deleteSessionResult = await daemonSessionStore.Delete(
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
            if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket
                && File.Exists(endpoint.Address))
            {
                FileUtilities.DeleteIfExists(endpoint.Address);
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
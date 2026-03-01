using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Resolves daemon session token values from persisted daemon session metadata. </summary>
internal sealed class DaemonSessionTokenProvider : IDaemonSessionTokenProvider
{
    private readonly IDaemonSessionStore daemonSessionStore;

    /// <summary> Initializes a new instance of the <see cref="DaemonSessionTokenProvider" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonSessionStore" /> is <see langword="null" />. </exception>
    public DaemonSessionTokenProvider (IDaemonSessionStore daemonSessionStore)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
    }

    /// <summary> Resolves one daemon session token value for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon session token resolution result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonSessionTokenResolutionResult> Resolve (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var readResult = await daemonSessionStore.Read(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return DaemonSessionTokenResolutionResult.Failure(readResult.Error!);
        }

        if (!readResult.Exists)
        {
            return DaemonSessionTokenResolutionResult.SessionNotAvailable();
        }

        var sessionToken = readResult.Session!.SessionToken;
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return DaemonSessionTokenResolutionResult.Failure(ExecutionError.InvalidArgument(
                "Daemon session token is missing."));
        }

        return DaemonSessionTokenResolutionResult.Success(sessionToken);
    }
}

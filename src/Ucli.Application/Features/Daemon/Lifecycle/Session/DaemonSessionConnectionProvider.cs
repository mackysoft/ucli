namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Resolves daemon IPC connection values from persisted daemon session metadata. </summary>
internal sealed class DaemonSessionConnectionProvider : IDaemonSessionConnectionProvider
{
    private readonly IDaemonSessionStore daemonSessionStore;

    /// <summary> Initializes a new instance of the <see cref="DaemonSessionConnectionProvider" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonSessionStore" /> is <see langword="null" />. </exception>
    public DaemonSessionConnectionProvider (IDaemonSessionStore daemonSessionStore)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
    }

    /// <inheritdoc />
    public async ValueTask<DaemonSessionConnectionResolutionResult> ResolveAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);

        var readResult = await daemonSessionStore.ReadAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return DaemonSessionConnectionResolutionResult.Failure(readResult.Error!);
        }

        if (!readResult.Exists)
        {
            return DaemonSessionConnectionResolutionResult.SessionNotAvailable();
        }

        var session = readResult.Session!;
        var connection = new DaemonSessionConnection(session.SessionToken, session.Endpoint);
        return DaemonSessionConnectionResolutionResult.Success(connection);
    }
}

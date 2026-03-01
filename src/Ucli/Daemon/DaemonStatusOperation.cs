using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements daemon status workflow orchestration for one project fingerprint. </summary>
internal sealed class DaemonStatusOperation : IDaemonStatusOperation
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonPingClient daemonPingClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    /// <summary> Initializes a new instance of the <see cref="DaemonStatusOperation" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="daemonPingClient"> The daemon ping client dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability classifier dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStatusOperation (
        IDaemonSessionStore daemonSessionStore,
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier reachabilityClassifier)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
    }

    /// <summary> Gets daemon status for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon status timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon status result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStatusResult> GetStatus (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var readResult = await daemonSessionStore.Read(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return DaemonStatusResult.Failure(readResult.Error!);
        }

        if (!readResult.Exists)
        {
            return DaemonStatusResult.NotRunning();
        }

        try
        {
            await daemonPingClient.Ping(
                    unityProject,
                    timeout,
                    readResult.Session!.SessionToken,
                    cancellationToken)
                .ConfigureAwait(false);
            return DaemonStatusResult.Running(readResult.Session!);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
        {
            return DaemonStatusResult.Stale(readResult.Session!);
        }
        catch (Exception exception)
        {
            return DaemonStatusResult.Failure(ExecutionError.InternalError(
                $"Failed to probe daemon status. {exception.Message}"));
        }
    }
}
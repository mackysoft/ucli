using MackySoft.Ucli.Daemon.Start;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements daemon start workflow orchestration for one project fingerprint. </summary>
internal sealed class DaemonStartOperation : IDaemonStartOperation
{
    private readonly IDaemonLifecycleLockProvider lifecycleLockProvider;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonPingClient daemonPingClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly IDaemonStartRecoveryService daemonStartRecoveryService;

    private readonly IDaemonStartLaunchService daemonStartLaunchService;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="daemonPingClient"> The daemon ping client dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability classifier dependency. </param>
    /// <param name="daemonStartRecoveryService"> The daemon-start recovery service dependency. </param>
    /// <param name="daemonStartLaunchService"> The daemon-start launch service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartOperation (
        IDaemonLifecycleLockProvider lifecycleLockProvider,
        IDaemonSessionStore daemonSessionStore,
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonStartRecoveryService daemonStartRecoveryService,
        IDaemonStartLaunchService daemonStartLaunchService)
    {
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.daemonStartRecoveryService = daemonStartRecoveryService ?? throw new ArgumentNullException(nameof(daemonStartRecoveryService));
        this.daemonStartLaunchService = daemonStartLaunchService ?? throw new ArgumentNullException(nameof(daemonStartLaunchService));
    }

    /// <summary> Starts daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon startup timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartResult> Start (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        await using var lockHandle = await lifecycleLockProvider.Acquire(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);

        var readResult = await daemonSessionStore.Read(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return await HandleInvalidSessionRead(unityProject, readResult, timeout, cancellationToken).ConfigureAwait(false);
        }

        if (readResult.Exists)
        {
            var startFromExistingResult = await StartFromExistingSession(unityProject, readResult.Session!, timeout, cancellationToken).ConfigureAwait(false);
            if (startFromExistingResult is not null)
            {
                return startFromExistingResult;
            }
        }

        return await daemonStartLaunchService.Launch(unityProject, timeout, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DaemonStartResult> HandleInvalidSessionRead (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (readResult.FailureKind != DaemonSessionReadFailureKind.InvalidSession)
        {
            return DaemonStartResult.Failure(readResult.Error!);
        }

        var recoveryResult = await daemonStartRecoveryService.RecoverInvalidSession(
                unityProject,
                readResult,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!recoveryResult.IsSuccess)
        {
            return DaemonStartResult.Failure(recoveryResult.Error!);
        }

        return await daemonStartLaunchService.Launch(unityProject, timeout, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DaemonStartResult?> StartFromExistingSession (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            await daemonPingClient.Ping(
                    unityProject,
                    timeout,
                    session.SessionToken,
                    cancellationToken)
                .ConfigureAwait(false);
            return DaemonStartResult.AlreadyRunning(session);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return DaemonStartResult.Failure(ExecutionError.Timeout(
                $"Timed out while probing existing daemon session. {exception.Message}"));
        }
        catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
        {
            var recoveryResult = await daemonStartRecoveryService.RecoverStaleSession(
                    unityProject,
                    session,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!recoveryResult.IsSuccess)
            {
                return DaemonStartResult.Failure(recoveryResult.Error!);
            }

            return null;
        }
        catch (Exception exception)
        {
            return DaemonStartResult.Failure(ExecutionError.InternalError(
                $"Failed to probe existing daemon session. {exception.Message}"));
        }
    }
}
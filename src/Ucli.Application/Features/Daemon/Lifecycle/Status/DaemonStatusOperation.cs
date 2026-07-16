using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

/// <summary> Implements daemon status workflow orchestration for one project fingerprint. </summary>
internal sealed class DaemonStatusOperation : IDaemonStatusOperation
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonDiagnosisStore daemonDiagnosisStore;

    private readonly IDaemonLaunchAttemptStore launchAttemptStore;

    private readonly DaemonSessionProbe daemonSessionProbe;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly IDaemonSessionDiagnosisResolver daemonSessionDiagnosisResolver;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonStatusOperation" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="daemonDiagnosisStore"> The daemon diagnosis store dependency. </param>
    /// <param name="launchAttemptStore"> The daemon launch-attempt store dependency. </param>
    /// <param name="daemonSessionProbe"> The exact-session probe and token-rotation dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability-classifier dependency. </param>
    /// <param name="daemonSessionDiagnosisResolver"> The daemon session-diagnosis resolver dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStatusOperation (
        IDaemonSessionStore daemonSessionStore,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        IDaemonLaunchAttemptStore launchAttemptStore,
        DaemonSessionProbe daemonSessionProbe,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonSessionDiagnosisResolver daemonSessionDiagnosisResolver,
        TimeProvider timeProvider)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
        this.launchAttemptStore = launchAttemptStore ?? throw new ArgumentNullException(nameof(launchAttemptStore));
        this.daemonSessionProbe = daemonSessionProbe ?? throw new ArgumentNullException(nameof(daemonSessionProbe));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.daemonSessionDiagnosisResolver = daemonSessionDiagnosisResolver ?? throw new ArgumentNullException(nameof(daemonSessionDiagnosisResolver));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Gets daemon status for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon status timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon status result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStatusResult> GetStatusAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var diagnosisReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before daemon diagnosis read could begin.",
                "Timed out while reading daemon diagnosis.",
                token => daemonDiagnosisStore.ReadAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    token))
            .ConfigureAwait(false);
        if (!diagnosisReadOperation.IsSuccess)
        {
            return DaemonStatusResult.Failure(diagnosisReadOperation.Error!);
        }

        var diagnosisReadResult = diagnosisReadOperation.Value!;
        var persistedDiagnosis = diagnosisReadResult.IsSuccess
            ? diagnosisReadResult.Diagnosis
            : null;

        var sessionReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before daemon session read could begin.",
                "Timed out while reading daemon session.",
                token => daemonSessionStore.ReadAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    token))
            .ConfigureAwait(false);
        if (!sessionReadOperation.IsSuccess)
        {
            return DaemonStatusResult.Failure(sessionReadOperation.Error!);
        }

        var readResult = sessionReadOperation.Value!;
        if (!readResult.IsSuccess)
        {
            return DaemonStatusResult.Failure(readResult.Error!);
        }

        if (!readResult.Exists)
        {
            var launchAttemptReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                    deadline,
                    cancellationToken,
                    "Timed out before daemon launch-attempt read could begin.",
                    "Timed out while reading daemon launch attempt.",
                    token => launchAttemptStore.ReadLastFailureAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        token))
                .ConfigureAwait(false);
            if (!launchAttemptReadOperation.IsSuccess)
            {
                return DaemonStatusResult.Failure(launchAttemptReadOperation.Error!);
            }

            var launchAttemptReadResult = launchAttemptReadOperation.Value!;
            if (!launchAttemptReadResult.IsSuccess)
            {
                return DaemonStatusResult.Failure(launchAttemptReadResult.Error!);
            }

            return DaemonStatusResult.NotRunning(persistedDiagnosis, launchAttemptReadResult.LaunchAttempt);
        }

        var probeResult = await daemonSessionProbe.ProbeAsync(
                unityProject,
                readResult.Session!,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (probeResult.SessionReadFailure is not null)
        {
            return DaemonStatusResult.Failure(probeResult.SessionReadFailure.Error!);
        }

        var probedSession = probeResult.Session;
        if (probeResult.IsSuccess)
        {
            var effectiveDiagnosis = Equals(probedSession, readResult.Session)
                ? persistedDiagnosis
                : null;
            return DaemonStatusResult.Running(
                probedSession,
                probeResult.PingResponse,
                effectiveDiagnosis);
        }

        var probeFailure = probeResult.ProbeFailure!;
        if (deadline.IsExpired)
        {
            return DaemonStatusResult.Failure(ExecutionError.Timeout(
                "Timed out while probing daemon status."));
        }

        if (reachabilityClassifier.IsRequestTimeout(probeFailure)
            || reachabilityClassifier.IsNotRunning(probeFailure))
        {
            return await ResolveStaleStatusAsync(
                    unityProject,
                    probedSession,
                    persistedDiagnosis,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return DaemonStatusResult.Failure(ExecutionError.InternalError(
            $"Failed to probe daemon status. {probeFailure.Message}"));
    }

    private async ValueTask<DaemonStatusResult> ResolveStaleStatusAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        DaemonDiagnosis? persistedDiagnosis,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        try
        {
            var diagnosisResolution = await ExecutionDeadlineOperation.ExecuteAsync(
                    deadline,
                    cancellationToken,
                    "Timed out before stale daemon diagnosis could begin.",
                    "Timed out while resolving stale daemon diagnosis.",
                    token => daemonSessionDiagnosisResolver.ResolveForSessionAsync(
                        unityProject,
                        session,
                        persistedDiagnosis,
                        token))
                .ConfigureAwait(false);
            if (!diagnosisResolution.IsSuccess)
            {
                return DaemonStatusResult.Failure(diagnosisResolution.Error!);
            }

            return DaemonStatusResult.Stale(session, diagnosisResolution.Value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (deadline.IsExpired)
            {
                return DaemonStatusResult.Failure(ExecutionError.Timeout(
                    "Timed out while resolving stale daemon diagnosis."));
            }

            return DaemonStatusResult.Failure(ExecutionError.InternalError(
                $"Failed to resolve stale daemon diagnosis. {exception.Message}"));
        }
    }
}

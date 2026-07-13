using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Projection;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;

/// <summary> Implements daemon-status command workflow orchestration. </summary>
internal sealed class DaemonStatusService : IDaemonStatusService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IDaemonStatusOperation daemonStatusOperation;

    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly IDaemonLifecycleStore daemonLifecycleStore;

    private readonly IDaemonProcessIdentityAssessor processIdentityAssessor;

    private readonly IDaemonSessionDiagnosisResolver daemonSessionDiagnosisResolver;

    private readonly IDaemonSessionOutputMapper daemonSessionOutputMapper;

    private readonly IDaemonDiagnosisOutputMapper daemonDiagnosisOutputMapper;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonStatusService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command execution-context resolver dependency. </param>
    /// <param name="daemonStatusOperation"> The daemon status-operation dependency. </param>
    /// <param name="daemonPingInfoClient"> The daemon ping-info client dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability classifier dependency. </param>
    /// <param name="daemonLifecycleStore"> The daemon lifecycle observation store dependency. </param>
    /// <param name="processIdentityAssessor"> The daemon process identity assessor dependency. </param>
    /// <param name="daemonSessionDiagnosisResolver"> The daemon session-diagnosis resolver dependency. </param>
    /// <param name="daemonSessionOutputMapper"> The daemon session-output mapper dependency. </param>
    /// <param name="daemonDiagnosisOutputMapper"> The daemon diagnosis-output mapper dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStatusService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IDaemonStatusOperation daemonStatusOperation,
        IDaemonPingInfoClient daemonPingInfoClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonLifecycleStore daemonLifecycleStore,
        IDaemonProcessIdentityAssessor processIdentityAssessor,
        IDaemonSessionDiagnosisResolver daemonSessionDiagnosisResolver,
        IDaemonSessionOutputMapper daemonSessionOutputMapper,
        IDaemonDiagnosisOutputMapper daemonDiagnosisOutputMapper,
        TimeProvider? timeProvider = null)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonStatusOperation = daemonStatusOperation ?? throw new ArgumentNullException(nameof(daemonStatusOperation));
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.daemonLifecycleStore = daemonLifecycleStore ?? throw new ArgumentNullException(nameof(daemonLifecycleStore));
        this.processIdentityAssessor = processIdentityAssessor ?? throw new ArgumentNullException(nameof(processIdentityAssessor));
        this.daemonSessionDiagnosisResolver = daemonSessionDiagnosisResolver ?? throw new ArgumentNullException(nameof(daemonSessionDiagnosisResolver));
        this.daemonSessionOutputMapper = daemonSessionOutputMapper ?? throw new ArgumentNullException(nameof(daemonSessionOutputMapper));
        this.daemonDiagnosisOutputMapper = daemonDiagnosisOutputMapper ?? throw new ArgumentNullException(nameof(daemonDiagnosisOutputMapper));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Executes one daemon-status workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeoutMilliseconds"> The optional normalized timeout value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-status execution result. </returns>
    public async ValueTask<DaemonStatusExecutionResult> GetStatusAsync (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.ResolveAsync(
                UcliCommandIds.DaemonStatus,
                projectPath,
                timeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return DaemonStatusExecutionResult.Failure(contextResult.Error!);
        }

        var executionContext = contextResult.Context!;
        var deadline = ExecutionDeadline.Start(executionContext.Timeout, timeProvider);
        if (!deadline.TryGetRemainingTimeout(out var statusTimeout))
        {
            return DaemonStatusExecutionResult.Failure(ExecutionError.Timeout(
                "Timed out before daemon status probe could begin."));
        }

        var statusResult = await daemonStatusOperation.GetStatusAsync(
                executionContext.Context.UnityProject,
                statusTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!statusResult.IsSuccess)
        {
            return DaemonStatusExecutionResult.Failure(statusResult.Error ?? ExecutionError.InternalError(
                "Daemon status operation failed without structured error details."));
        }

        if (!IsSupportedDaemonStatus(statusResult.Status))
        {
            return DaemonStatusExecutionResult.Failure(ExecutionError.InternalError(
                $"Daemon status returned unsupported status: {statusResult.Status}."));
        }

        var daemonObservation = StatusDaemonObservationCodec.CreateWithoutPing(statusResult.Status);
        var persistedDiagnosis = statusResult.Diagnosis;
        var diagnosis = statusResult.Status == DaemonStatusKind.Running
            ? null
            : persistedDiagnosis;

        if (statusResult.Status == DaemonStatusKind.Stale && statusResult.Session is not null)
        {
            var unreachableObservation = await CreateUnreachableObservationAsync(
                    executionContext.Context.UnityProject,
                    statusResult.Session,
                    cancellationToken)
                .ConfigureAwait(false);
            daemonObservation = unreachableObservation;
            diagnosis = daemonObservation.DaemonStatus == DaemonStatusKind.Running
                ? null
                : diagnosis;
        }

        if (statusResult.Status == DaemonStatusKind.Running)
        {
            if (statusResult.Session is null)
            {
                return DaemonStatusExecutionResult.Failure(ExecutionError.InternalError(
                    "Daemon status is running but daemon session is missing."));
            }

            if (!deadline.TryGetRemainingTimeout(out var pingInfoTimeout))
            {
                return DaemonStatusExecutionResult.Failure(ExecutionError.Timeout(
                    "Timed out before daemon ping information read could begin."));
            }

            try
            {
                var pingResponse = await daemonPingInfoClient.PingAndReadAsync(
                        executionContext.Context.UnityProject,
                        pingInfoTimeout,
                        statusResult.Session.SessionToken,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                daemonObservation = StatusDaemonObservationCodec.CreateFromPing(statusResult.Status, pingResponse);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException)
            {
                var unreachableResolution = await ResolveUnreachableRunningSessionAsync(
                        executionContext.Context.UnityProject,
                        statusResult.Session,
                        persistedDiagnosis,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!unreachableResolution.IsSuccess)
                {
                    return DaemonStatusExecutionResult.Failure(unreachableResolution.Error!);
                }

                daemonObservation = unreachableResolution.Observation!;
                diagnosis = unreachableResolution.Diagnosis;
            }
            catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
            {
                var unreachableResolution = await ResolveUnreachableRunningSessionAsync(
                        executionContext.Context.UnityProject,
                        statusResult.Session,
                        persistedDiagnosis,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!unreachableResolution.IsSuccess)
                {
                    return DaemonStatusExecutionResult.Failure(unreachableResolution.Error!);
                }

                daemonObservation = unreachableResolution.Observation!;
                diagnosis = unreachableResolution.Diagnosis;
            }
            catch (Exception exception)
            {
                return DaemonStatusExecutionResult.Failure(ExecutionError.InternalError(
                    $"Failed to read daemon ping information. {exception.Message}"));
            }
        }

        var output = new DaemonStatusExecutionOutput(
            DaemonStatus: daemonObservation.DaemonStatus,
            ServerVersion: daemonObservation.ServerVersion,
            EditorMode: daemonObservation.EditorMode,
            LifecycleState: daemonObservation.LifecycleState,
            BlockingReason: daemonObservation.BlockingReason,
            CompileState: daemonObservation.CompileState,
            Generations: daemonObservation.Generations,
            CanAcceptExecutionRequests: daemonObservation.CanAcceptExecutionRequests,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds),
            Session: statusResult.Session is null
                ? null
                : daemonSessionOutputMapper.ToOutput(statusResult.Session),
            Diagnosis: diagnosis is null
                ? null
                : daemonDiagnosisOutputMapper.ToOutput(diagnosis),
            LastLaunchAttempt: statusResult.LastLaunchAttempt is null || statusResult.Session is not null
                ? null
                : ToLaunchAttemptOutput(statusResult.LastLaunchAttempt),
            ObservedAtUtc: daemonObservation.ObservedAtUtc,
            ActionRequired: daemonObservation.ActionRequired,
            PrimaryDiagnostic: daemonObservation.PrimaryDiagnostic,
            PlayMode: daemonObservation.PlayMode);
        return DaemonStatusExecutionResult.Success(output);
    }

    private DaemonLaunchAttemptOutput ToLaunchAttemptOutput (DaemonLaunchAttempt launchAttempt)
    {
        ArgumentNullException.ThrowIfNull(launchAttempt);
        return new DaemonLaunchAttemptOutput(
            LaunchAttemptId: launchAttempt.LaunchAttemptId,
            StartupStatus: launchAttempt.StartupStatus,
            StartupBlockingReason: launchAttempt.StartupBlockingReason,
            RetryDisposition: launchAttempt.RetryDisposition,
            ProcessAction: launchAttempt.ProcessAction,
            ArtifactPath: launchAttempt.ArtifactPath,
            UnityLogPath: launchAttempt.UnityLogPath,
            UpdatedAtUtc: launchAttempt.UpdatedAtUtc,
            ProcessId: launchAttempt.ProcessId,
            ProcessStartedAtUtc: launchAttempt.ProcessStartedAtUtc,
            Diagnosis: daemonDiagnosisOutputMapper.ToOutput(launchAttempt.Diagnosis));
    }

    private async ValueTask<UnreachableDaemonStatusResolution> ResolveUnreachableRunningSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        DaemonDiagnosis? persistedDiagnosis,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var unreachableObservation = await CreateUnreachableObservationAsync(
                unityProject,
                session,
                cancellationToken)
            .ConfigureAwait(false);
        if (unreachableObservation.DaemonStatus == DaemonStatusKind.Running)
        {
            return UnreachableDaemonStatusResolution.Success(unreachableObservation, diagnosis: null);
        }

        if (!deadline.TryGetRemainingTimeout(out var diagnosisTimeout))
        {
            return UnreachableDaemonStatusResolution.Failure(ExecutionError.Timeout(
                "Timed out before stale daemon diagnosis could begin."));
        }

        using var diagnosisCancellationScope = TimeProviderCancellationScope.CreateLinked(
            cancellationToken,
            diagnosisTimeout,
            timeProvider);

        try
        {
            var diagnosis = await daemonSessionDiagnosisResolver.ResolveForSessionAsync(
                    unityProject,
                    session,
                    persistedDiagnosis,
                    diagnosisCancellationScope.Token)
                .ConfigureAwait(false);
            return UnreachableDaemonStatusResolution.Success(unreachableObservation, diagnosis);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (diagnosisCancellationScope.HasTimedOut
            && !cancellationToken.IsCancellationRequested)
        {
            return UnreachableDaemonStatusResolution.Failure(ExecutionError.Timeout(
                "Timed out while resolving stale daemon diagnosis."));
        }
        catch (Exception diagnosisException)
        {
            return UnreachableDaemonStatusResolution.Failure(ExecutionError.InternalError(
                $"Failed to resolve stale daemon diagnosis. {diagnosisException.Message}"));
        }
    }

    private async ValueTask<StatusDaemonObservation> CreateUnreachableObservationAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        CancellationToken cancellationToken)
    {
        var lifecycleReadResult = await daemonLifecycleStore.ReadAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        var observation = lifecycleReadResult.Observation;
        if (lifecycleReadResult.IsSuccess
            && lifecycleReadResult.Exists
            && observation is not null
            && DaemonLifecycleObservationAvailability.IsUsableForSession(
                observation,
                session,
                processIdentityAssessor,
                timeProvider))
        {
            return StatusDaemonObservationCodec.CreateFromLifecycleObservation(
                DaemonStatusKind.Running,
                observation);
        }

        return StatusDaemonObservationCodec.CreateUnavailable(DaemonStatusKind.Stale);
    }
    private static bool IsSupportedDaemonStatus (DaemonStatusKind status)
    {
        return status is DaemonStatusKind.Running
            or DaemonStatusKind.NotRunning
            or DaemonStatusKind.Stale;
    }

    private sealed record UnreachableDaemonStatusResolution (
        StatusDaemonObservation? Observation,
        DaemonDiagnosis? Diagnosis,
        ExecutionError? Error)
    {
        public bool IsSuccess => Observation is not null && Error is null;

        public static UnreachableDaemonStatusResolution Success (
            StatusDaemonObservation observation,
            DaemonDiagnosis? diagnosis)
        {
            return new UnreachableDaemonStatusResolution(observation, diagnosis, null);
        }

        public static UnreachableDaemonStatusResolution Failure (ExecutionError error)
        {
            return new UnreachableDaemonStatusResolution(null, null, error);
        }
    }
}

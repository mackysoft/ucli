using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Projection;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;

/// <summary> Implements daemon-status command workflow orchestration. </summary>
internal sealed class DaemonStatusService : IDaemonStatusService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IDaemonStatusOperation daemonStatusOperation;

    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly IDaemonSessionDiagnosisResolver daemonSessionDiagnosisResolver;

    private readonly IDaemonSessionOutputMapper daemonSessionOutputMapper;

    private readonly IDaemonDiagnosisOutputMapper daemonDiagnosisOutputMapper;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonStatusService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command execution-context resolver dependency. </param>
    /// <param name="daemonStatusOperation"> The daemon status-operation dependency. </param>
    /// <param name="daemonPingInfoClient"> The daemon ping-info client dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability classifier dependency. </param>
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
        IDaemonSessionDiagnosisResolver daemonSessionDiagnosisResolver,
        IDaemonSessionOutputMapper daemonSessionOutputMapper,
        IDaemonDiagnosisOutputMapper daemonDiagnosisOutputMapper,
        TimeProvider? timeProvider = null)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonStatusOperation = daemonStatusOperation ?? throw new ArgumentNullException(nameof(daemonStatusOperation));
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
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
    public async ValueTask<DaemonStatusExecutionResult> GetStatus (
        string? projectPath,
        int? timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.Resolve(
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

        var statusResult = await daemonStatusOperation.GetStatus(
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

        var daemonStatus = statusResult.Status;
        var serverVersion = (string?)null;
        var runtime = (string?)null;
        var lifecycleState = (string?)null;
        var blockingReason = (string?)null;
        var compileState = (string?)null;
        var compileGeneration = (string?)null;
        var domainReloadGeneration = (string?)null;
        var canAcceptExecutionRequests = false;
        var persistedDiagnosis = statusResult.Diagnosis;
        var diagnosis = statusResult.Status == DaemonStatusKind.Running
            ? null
            : persistedDiagnosis;

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
                var pingResponse = await daemonPingInfoClient.PingAndRead(
                        executionContext.Context.UnityProject,
                        pingInfoTimeout,
                        statusResult.Session.SessionToken,
                        cancellationToken)
                    .ConfigureAwait(false);
                var observation = StatusDaemonObservationCodec.CreateFromPing(statusResult.Status, pingResponse);
                serverVersion = observation.ServerVersion;
                runtime = observation.Runtime;
                lifecycleState = observation.LifecycleState;
                blockingReason = observation.BlockingReason;
                compileState = observation.CompileState;
                compileGeneration = observation.CompileGeneration;
                domainReloadGeneration = observation.DomainReloadGeneration;
                canAcceptExecutionRequests = observation.CanAcceptExecutionRequests;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException exception)
            {
                return DaemonStatusExecutionResult.Failure(ExecutionError.Timeout(
                    $"Timed out while reading daemon ping information. {exception.Message}"));
            }
            catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
            {
                if (!deadline.TryGetRemainingTimeout(out var diagnosisTimeout))
                {
                    return DaemonStatusExecutionResult.Failure(ExecutionError.Timeout(
                        "Timed out before stale daemon diagnosis could begin."));
                }

                using var diagnosisCancellationScope = TimeProviderCancellationScope.CreateLinked(
                    cancellationToken,
                    diagnosisTimeout,
                    timeProvider);

                daemonStatus = DaemonStatusKind.Stale;
                try
                {
                    diagnosis = await daemonSessionDiagnosisResolver.ResolveForSession(
                            executionContext.Context.UnityProject,
                            statusResult.Session,
                            persistedDiagnosis,
                            diagnosisCancellationScope.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (diagnosisCancellationScope.HasTimedOut
                    && !cancellationToken.IsCancellationRequested)
                {
                    return DaemonStatusExecutionResult.Failure(ExecutionError.Timeout(
                        "Timed out while resolving stale daemon diagnosis."));
                }
                catch (Exception diagnosisException)
                {
                    return DaemonStatusExecutionResult.Failure(ExecutionError.InternalError(
                        $"Failed to resolve stale daemon diagnosis. {diagnosisException.Message}"));
                }
            }
            catch (Exception exception)
            {
                return DaemonStatusExecutionResult.Failure(ExecutionError.InternalError(
                    $"Failed to read daemon ping information. {exception.Message}"));
            }
        }

        var output = new DaemonStatusExecutionOutput(
            DaemonStatus: daemonStatus,
            ServerVersion: serverVersion,
            Runtime: runtime,
            LifecycleState: lifecycleState,
            BlockingReason: blockingReason,
            CompileState: compileState,
            CompileGeneration: compileGeneration,
            DomainReloadGeneration: domainReloadGeneration,
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds),
            Session: statusResult.Session is null
                ? null
                : daemonSessionOutputMapper.ToOutput(statusResult.Session),
            Diagnosis: diagnosis is null
                ? null
                : daemonDiagnosisOutputMapper.ToOutput(diagnosis));
        return DaemonStatusExecutionResult.Success(output);
    }

    private static bool IsSupportedDaemonStatus (DaemonStatusKind status)
    {
        return status is DaemonStatusKind.Running
            or DaemonStatusKind.NotRunning
            or DaemonStatusKind.Stale;
    }
}

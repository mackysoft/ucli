using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Supervisor;

/// <summary> Verifies that one started Unity daemon remains reachable across the supervisor stability window. </summary>
internal sealed class SupervisorStabilityVerifier
{
    private readonly IDaemonPingClient daemonPingClient;

    private readonly SupervisorDiagnosisWriter diagnosisWriter;

    /// <summary> Initializes a new instance of the <see cref="SupervisorStabilityVerifier" /> class. </summary>
    /// <param name="daemonPingClient"> The daemon ping-client dependency. </param>
    /// <param name="daemonStopOperation"> The daemon stop-operation dependency. </param>
    /// <param name="diagnosisWriter"> The supervisor diagnosis-writer dependency. </param>
    public SupervisorStabilityVerifier (
        IDaemonPingClient daemonPingClient,
        SupervisorDiagnosisWriter diagnosisWriter)
    {
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.diagnosisWriter = diagnosisWriter ?? throw new ArgumentNullException(nameof(diagnosisWriter));
    }

    /// <summary> Ensures that one started daemon stays reachable for the fixed supervisor stability window. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The daemon session returned by daemon start. </param>
    /// <param name="timeout"> The remaining command timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The stability-verification result. </returns>
    public async ValueTask<SupervisorStabilityVerificationResult> EnsureStable (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var stabilityBudget = timeout < SupervisorConstants.StabilityWindow
            ? timeout
            : SupervisorConstants.StabilityWindow;
        var stabilityDeadline = ExecutionDeadline.Start(stabilityBudget);
        var successCount = 0;
        var retryDelay = TimeSpan.FromMilliseconds(
            Math.Max(1, (int)Math.Ceiling(
                stabilityBudget.TotalMilliseconds
                / SupervisorConstants.StabilitySuccessCount)));

        while (successCount < SupervisorConstants.StabilitySuccessCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!stabilityDeadline.TryGetRemainingTimeout(out var remainingStabilityTimeout))
            {
                return await FailStabilityTimeoutCheck(unityProject, session).ConfigureAwait(false);
            }

            var attemptTimeout = remainingStabilityTimeout < SupervisorConstants.PingTimeout
                ? remainingStabilityTimeout
                : SupervisorConstants.PingTimeout;

            try
            {
                using var pingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                pingCancellationTokenSource.CancelAfter(attemptTimeout);
                await daemonPingClient.Ping(
                        unityProject,
                        attemptTimeout,
                        session.SessionToken,
                        pingCancellationTokenSource.Token)
                    .ConfigureAwait(false);
                successCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return await FailStabilityTimeoutCheck(unityProject, session).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return await FailStabilityCheck(
                        unityProject,
                        session,
                        ExecutionError.InternalError(
                            $"Unity daemon failed the supervisor stability window. {exception.Message}"))
                    .ConfigureAwait(false);
            }

            if (successCount < SupervisorConstants.StabilitySuccessCount)
            {
                if (!stabilityDeadline.TryGetRemainingTimeout(out var remainingDelayTimeout))
                {
                    return await FailStabilityTimeoutCheck(unityProject, session).ConfigureAwait(false);
                }

                var delay = remainingDelayTimeout < retryDelay
                    ? remainingDelayTimeout
                    : retryDelay;
                if (delay <= TimeSpan.Zero)
                {
                    return await FailStabilityTimeoutCheck(unityProject, session).ConfigureAwait(false);
                }

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        return SupervisorStabilityVerificationResult.Success();
    }

    private async ValueTask<SupervisorStabilityVerificationResult> FailStabilityCheck (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionError primaryError)
    {
        ArgumentNullException.ThrowIfNull(primaryError);

        var effectiveError = primaryError;
        var diagnosisWriteResult = await diagnosisWriter.WriteUnexpected(
                unityProject,
                session,
                DaemonDiagnosisReasonValues.StartupUnstable,
                effectiveError.Message,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (!diagnosisWriteResult.IsSuccess)
        {
            effectiveError = CreateAugmentedPrimaryError(
                effectiveError,
                diagnosisWriteResult.Error,
                "DiagnosisError");
        }

        return SupervisorStabilityVerificationResult.Failure(effectiveError);
    }

    private async ValueTask<SupervisorStabilityVerificationResult> FailStabilityTimeoutCheck (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session)
    {
        const string Message = "Unity daemon stability verification exceeded the remaining timeout.";

        var timeoutError = ExecutionError.Timeout(Message);
        var diagnosisWriteResult = await diagnosisWriter.WriteUnexpected(
                unityProject,
                session,
                DaemonDiagnosisReasonValues.StartupUnstable,
                Message,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (!diagnosisWriteResult.IsSuccess)
        {
            timeoutError = CreateAugmentedPrimaryError(
                timeoutError,
                diagnosisWriteResult.Error,
                "DiagnosisError");
        }

        // NOTE:
        // timeout must be reported within the caller budget. compensation stop is scheduled by the coordinator.
        return SupervisorStabilityVerificationResult.Failure(timeoutError);
    }

    private static ExecutionError CreateAugmentedPrimaryError (
        ExecutionError primaryError,
        ExecutionError? supplementalError,
        string label)
    {
        ArgumentNullException.ThrowIfNull(primaryError);
        ArgumentNullException.ThrowIfNull(supplementalError);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var message =
            "Supervisor stability verification failed and follow-up handling did not complete. " +
            $"PrimaryError={primaryError.Message} " +
            $"{label}={supplementalError.Message}";

        return primaryError.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => ExecutionError.InvalidArgument(message),
            ExecutionErrorKind.Timeout => ExecutionError.Timeout(message),
            ExecutionErrorKind.InternalError => ExecutionError.InternalError(message),
            _ => throw new ArgumentOutOfRangeException(nameof(primaryError), primaryError.Kind, "Unsupported execution error kind."),
        };
    }
}
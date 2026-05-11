using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Observes one CLI-launched GUI Unity Editor startup attempt until session registration or a terminal blocker is found. </summary>
internal sealed class DaemonGuiStartupObserver : IDaemonGuiStartupObserver
{
    private readonly IDaemonGuiSessionRegistrationAwaiter sessionRegistrationAwaiter;

    private readonly IUnityLogReader unityLogReader;

    private readonly IDaemonProcessIdentityAssessor processIdentityAssessor;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonGuiStartupObserver" /> class. </summary>
    public DaemonGuiStartupObserver (
        IDaemonGuiSessionRegistrationAwaiter sessionRegistrationAwaiter,
        IUnityLogReader unityLogReader,
        IDaemonProcessIdentityAssessor processIdentityAssessor,
        TimeProvider? timeProvider = null)
    {
        this.sessionRegistrationAwaiter = sessionRegistrationAwaiter ?? throw new ArgumentNullException(nameof(sessionRegistrationAwaiter));
        this.unityLogReader = unityLogReader ?? throw new ArgumentNullException(nameof(unityLogReader));
        this.processIdentityAssessor = processIdentityAssessor ?? throw new ArgumentNullException(nameof(processIdentityAssessor));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<DaemonGuiStartupObservationResult> WaitForStartupAsync (
        ResolvedUnityProjectContext unityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        string unityLogPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(processId, 0);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityLogPath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return await CreateTimeoutResultAsync(
                        unityProject,
                        processId,
                        processStartedAtUtc,
                        unityLogPath,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var sessionResult = await sessionRegistrationAwaiter.WaitForSessionAsync(
                    unityProject,
                    processId,
                    GetObservationAttemptTimeout(remainingTimeout),
                    expectedProcessStartedAtUtc: processStartedAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            if (sessionResult.IsSuccess)
            {
                return DaemonGuiStartupObservationResult.Success(sessionResult.Session!);
            }

            if (sessionResult.Error!.Kind != ExecutionErrorKind.Timeout)
            {
                return DaemonGuiStartupObservationResult.Failure(sessionResult.Error);
            }

            var logBlocker = await TryClassifyLogAsync(
                    unityProject,
                    processId,
                    processStartedAtUtc,
                    unityLogPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (logBlocker is not null)
            {
                return DaemonGuiStartupObservationResult.Blocked(logBlocker);
            }

            if (IsExpectedProcessStillAlive(processId, processStartedAtUtc))
            {
                continue;
            }

            logBlocker = await TryClassifyLogAsync(
                    unityProject,
                    processId,
                    processStartedAtUtc,
                    unityLogPath,
                    cancellationToken)
                .ConfigureAwait(false);
            return DaemonGuiStartupObservationResult.Blocked(logBlocker ?? CreateProcessExitedBlocker(
                processId,
                processStartedAtUtc,
                unityLogPath));
        }
    }

    private bool IsExpectedProcessStillAlive (
        int processId,
        DateTimeOffset processStartedAtUtc)
    {
        var assessment = processIdentityAssessor.AssessByProcessId(processId, processStartedAtUtc);
        return assessment.Status is DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess
            or DaemonProcessIdentityAssessmentStatus.Uncertain;
    }

    private async ValueTask<DaemonGuiStartupObservationResult> CreateTimeoutResultAsync (
        ResolvedUnityProjectContext unityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        string unityLogPath,
        CancellationToken cancellationToken)
    {
        var logBlocker = await TryClassifyLogAsync(
                unityProject,
                processId,
                processStartedAtUtc,
                unityLogPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (logBlocker is not null)
        {
            return DaemonGuiStartupObservationResult.Blocked(logBlocker);
        }

        return DaemonGuiStartupObservationResult.Failure(ExecutionError.Timeout(
            $"Timed out while waiting for GUI daemon session registration. ProcessId={processId}.",
            ExecutionErrorCodes.IpcTimeout));
    }

    private async ValueTask<DaemonGuiStartupBlocker?> TryClassifyLogAsync (
        ResolvedUnityProjectContext unityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        string unityLogPath,
        CancellationToken cancellationToken)
    {
        var logReadResult = await unityLogReader.ReadTailAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!logReadResult.IsSuccess || string.IsNullOrWhiteSpace(logReadResult.Text))
        {
            return null;
        }

        var latestStartupLogText = DaemonStartupFailureLogClassifier.GetLatestStartupLogText(logReadResult.Text);
        if (!DaemonStartupFailureLogClassifier.TryClassifyFailure(
                latestStartupLogText,
                DaemonStartupFailureClassificationContext.Gui,
                out var classification))
        {
            return null;
        }

        return new DaemonGuiStartupBlocker(
            StartupBlockingReason: classification!.StartupBlockingReason,
            Reason: classification!.Reason,
            RetryDisposition: classification.RetryDisposition,
            Message: classification.Message,
            StartupPhase: classification.StartupPhase,
            ActionRequired: classification.ActionRequired,
            ProcessId: processId,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: string.IsNullOrWhiteSpace(logReadResult.Path) ? unityLogPath : logReadResult.Path,
            PrimaryDiagnostic: classification.PrimaryDiagnostic);
    }

    private static TimeSpan GetObservationAttemptTimeout (TimeSpan remainingTimeout)
    {
        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        return remainingTimeout < retryDelay
            ? remainingTimeout
            : retryDelay;
    }

    private static DaemonGuiStartupBlocker CreateProcessExitedBlocker (
        int processId,
        DateTimeOffset processStartedAtUtc,
        string unityLogPath)
    {
        var message = $"Unity Editor process exited before GUI daemon session registration. ProcessId={processId}.";
        return new DaemonGuiStartupBlocker(
            StartupBlockingReason: DaemonStartupBlockingReasonValues.ProcessExit,
            Reason: DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap,
            RetryDisposition: DaemonStartupRetryDispositionValues.Unknown,
            Message: message,
            StartupPhase: DaemonDiagnosisStartupPhaseValues.ProcessExit,
            ActionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog,
            ProcessId: processId,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath,
            PrimaryDiagnostic: new DaemonPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.ProcessExit,
                Code: null,
                File: null,
                Line: null,
                Column: null,
                Message: message));
    }
}

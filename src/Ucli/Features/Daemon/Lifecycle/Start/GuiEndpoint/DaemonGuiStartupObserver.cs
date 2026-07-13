using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;
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
        TimeProvider timeProvider)
    {
        this.sessionRegistrationAwaiter = sessionRegistrationAwaiter ?? throw new ArgumentNullException(nameof(sessionRegistrationAwaiter));
        this.unityLogReader = unityLogReader ?? throw new ArgumentNullException(nameof(unityLogReader));
        this.processIdentityAssessor = processIdentityAssessor ?? throw new ArgumentNullException(nameof(processIdentityAssessor));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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
                return CreateTimeoutResult(processId);
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
                return DaemonGuiStartupObservationResult.Success(
                    sessionResult.Session!,
                    sessionResult.LifecycleObservation);
            }

            if (sessionResult.Error!.Kind != ExecutionErrorKind.Timeout)
            {
                return DaemonGuiStartupObservationResult.Failure(sessionResult.Error);
            }

            var logClassification = await TryClassifyLogAsync(
                    unityProject,
                    processId,
                    processStartedAtUtc,
                    unityLogPath,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (logClassification.DeadlineExpired)
            {
                return CreateTimeoutResult(processId);
            }

            if (logClassification.Blocker is not null)
            {
                return DaemonGuiStartupObservationResult.Blocked(logClassification.Blocker);
            }

            if (IsExpectedProcessStillAlive(processId, processStartedAtUtc))
            {
                continue;
            }

            logClassification = await TryClassifyLogAsync(
                    unityProject,
                    processId,
                    processStartedAtUtc,
                    unityLogPath,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            return DaemonGuiStartupObservationResult.Blocked(logClassification.Blocker ?? CreateProcessExitedBlocker(
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

    private static DaemonGuiStartupObservationResult CreateTimeoutResult (int processId)
    {
        return DaemonGuiStartupObservationResult.Failure(ExecutionError.Timeout(
            $"Timed out while waiting for GUI daemon session registration. ProcessId={processId}.",
            ExecutionErrorCodes.IpcTimeout));
    }

    private async ValueTask<(DaemonGuiStartupBlockerObservation? Blocker, bool DeadlineExpired)> TryClassifyLogAsync (
        ResolvedUnityProjectContext unityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        string unityLogPath,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var logReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before Unity startup log read could begin.",
                "Timed out while reading the Unity startup log.",
                token => unityLogReader.ReadTailAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    cancellationToken: token))
            .ConfigureAwait(false);
        if (!logReadOperation.IsSuccess)
        {
            return (null, true);
        }

        var logReadResult = logReadOperation.Value!;
        if (!logReadResult.IsSuccess || string.IsNullOrWhiteSpace(logReadResult.Text))
        {
            return (null, false);
        }

        var latestStartupLogText = DaemonStartupFailureLogClassifier.GetLatestStartupLogText(logReadResult.Text);
        if (!DaemonStartupFailureLogClassifier.TryClassifyFailure(
                latestStartupLogText,
                DaemonStartupFailureClassificationContext.Gui,
                out var classification))
        {
            return (null, false);
        }

        return (new DaemonGuiStartupBlockerObservation(
            classification,
            processId,
            processStartedAtUtc,
            string.IsNullOrWhiteSpace(logReadResult.Path) ? unityLogPath : logReadResult.Path), false);
    }

    private static TimeSpan GetObservationAttemptTimeout (TimeSpan remainingTimeout)
    {
        return remainingTimeout < DaemonTimeouts.ProbeAttemptTimeoutCap
            ? remainingTimeout
            : DaemonTimeouts.ProbeAttemptTimeoutCap;
    }

    private static DaemonGuiStartupBlockerObservation CreateProcessExitedBlocker (
        int processId,
        DateTimeOffset processStartedAtUtc,
        string unityLogPath)
    {
        var message = $"Unity Editor process exited before GUI daemon session registration. ProcessId={processId}.";
        return new DaemonGuiStartupBlockerObservation(
            new DaemonStartupFailureClassification(
                startupBlockingReason: DaemonStartupBlockingReason.ProcessExit,
                reason: DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap,
                retryDisposition: DaemonStartupRetryDisposition.Unknown,
                message: message,
                startupPhase: DaemonDiagnosisStartupPhase.ProcessExit,
                actionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog,
                primaryDiagnostic: new DaemonPrimaryDiagnostic(
                    Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.ProcessExit,
                    Code: null,
                    File: null,
                    Line: null,
                    Column: null,
                    Message: message)),
            processId,
            processStartedAtUtc,
            unityLogPath);
    }
}

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Launch;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Shutdown;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Launch;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start.Launch;

/// <summary> Implements daemon launch workflow with failure-compensation handling. </summary>
internal sealed class DaemonLaunchService : IDaemonLaunchService
{
    private readonly IDaemonLaunchSessionService daemonLaunchSessionService;

    private readonly IUnityDaemonProcessLauncher unityDaemonProcessLauncher;

    private readonly IUnityGuiEditorProcessLauncher unityGuiEditorProcessLauncher;

    private readonly IDaemonStartupReadinessProbe startupReadinessProbe;

    private readonly IDaemonGuiStartupObserver guiStartupObserver;

    private readonly IDaemonLaunchCompensationService daemonLaunchCompensationService;

    private readonly IDaemonDiagnosisStore daemonDiagnosisStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonLaunchService" /> class. </summary>
    /// <param name="daemonLaunchSessionService"> The daemon launch-session service dependency. </param>
    /// <param name="unityDaemonProcessLauncher"> The Unity daemon process-launcher dependency. </param>
    /// <param name="unityGuiEditorProcessLauncher"> The Unity GUI Editor process-launcher dependency. </param>
    /// <param name="startupReadinessProbe"> The daemon startup-readiness probe dependency. </param>
    /// <param name="guiStartupObserver"> The GUI startup observer dependency. </param>
    /// <param name="daemonLaunchCompensationService"> The daemon launch-compensation service dependency. </param>
    /// <param name="daemonDiagnosisStore"> The daemon diagnosis store dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting and timestamps. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonLaunchService (
        IDaemonLaunchSessionService daemonLaunchSessionService,
        IUnityDaemonProcessLauncher unityDaemonProcessLauncher,
        IUnityGuiEditorProcessLauncher unityGuiEditorProcessLauncher,
        IDaemonStartupReadinessProbe startupReadinessProbe,
        IDaemonGuiStartupObserver guiStartupObserver,
        IDaemonLaunchCompensationService daemonLaunchCompensationService,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        TimeProvider? timeProvider = null)
    {
        this.daemonLaunchSessionService = daemonLaunchSessionService ?? throw new ArgumentNullException(nameof(daemonLaunchSessionService));
        this.unityDaemonProcessLauncher = unityDaemonProcessLauncher ?? throw new ArgumentNullException(nameof(unityDaemonProcessLauncher));
        this.unityGuiEditorProcessLauncher = unityGuiEditorProcessLauncher ?? throw new ArgumentNullException(nameof(unityGuiEditorProcessLauncher));
        this.startupReadinessProbe = startupReadinessProbe ?? throw new ArgumentNullException(nameof(startupReadinessProbe));
        this.guiStartupObserver = guiStartupObserver ?? throw new ArgumentNullException(nameof(guiStartupObserver));
        this.daemonLaunchCompensationService = daemonLaunchCompensationService ?? throw new ArgumentNullException(nameof(daemonLaunchCompensationService));
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Launches daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon startup timeout. </param>
    /// <param name="editorMode"> The requested daemon Editor mode. </param>
    /// <param name="onStartupBlocked"> The startup-blocked process policy requested by the caller. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);

        return editorMode switch
        {
            DaemonEditorMode.Batchmode => await LaunchBatchmodeAsync(
                    unityProject,
                    editorMode,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false),
            DaemonEditorMode.Gui => await LaunchGuiAsync(
                    unityProject,
                    deadline,
                    onStartupBlocked,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => DaemonStartResult.Failure(ExecutionError.InvalidArgument(
                $"daemon start editorMode is invalid. Actual: {editorMode}.")),
        };
    }

    private async ValueTask<DaemonStartResult> LaunchBatchmodeAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonEditorMode editorMode,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var initializeSessionResult = await daemonLaunchSessionService.InitializeAsync(
                unityProject,
                editorMode,
                cancellationToken)
            .ConfigureAwait(false);
        if (!initializeSessionResult.IsSuccess)
        {
            return DaemonStartResult.Failure(initializeSessionResult.Error!);
        }
        var session = initializeSessionResult.Session!;
        var launchedProcessId = default(int?);
        var launchedProcessStartedAtUtc = default(DateTimeOffset?);
        var expectedIssuedAtUtc = session.IssuedAtUtc;

        try
        {
            var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint);
            var launchResult = await unityDaemonProcessLauncher.LaunchAsync(
                    unityProject,
                    session,
                    unityLogPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!launchResult.IsSuccess)
            {
                return await CreateFailureWithCompensationAsync(
                        unityProject,
                        launchResult.ProcessId,
                        launchResult.ProcessStartedAtUtc,
                        expectedIssuedAtUtc,
                        launchResult.Error!,
                        "Daemon launch failed",
                        "LaunchError")
                    .ConfigureAwait(false);
            }

            var processId = launchResult.ProcessId!.Value;
            launchedProcessId = processId;
            launchedProcessStartedAtUtc = launchResult.ProcessStartedAtUtc!.Value;
            var updateProcessIdResult = await daemonLaunchSessionService.UpdateProcessIdAsync(
                    unityProject,
                    session,
                    launchedProcessId,
                    launchedProcessStartedAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!updateProcessIdResult.IsSuccess)
            {
                return await CreateFailureWithCompensationAsync(
                        unityProject,
                        launchedProcessId,
                        launchedProcessStartedAtUtc,
                        expectedIssuedAtUtc,
                        updateProcessIdResult.Error!,
                        "Daemon session update failed",
                        "SessionError")
                    .ConfigureAwait(false);
            }

            session = updateProcessIdResult.Session!;
            expectedIssuedAtUtc = session.IssuedAtUtc;
            if (!deadline.TryGetRemainingTimeout(out var probeTimeout))
            {
                return await CreateFailureWithCompensationAsync(
                        unityProject,
                        launchedProcessId,
                        launchedProcessStartedAtUtc,
                        expectedIssuedAtUtc,
                        ExecutionError.Timeout("Timed out before daemon startup readiness probe could begin."),
                        "Daemon startup readiness probe failed",
                        "ProbeError")
                    .ConfigureAwait(false);
            }

            var probeResult = await startupReadinessProbe.WaitUntilReadyAsync(
                    unityProject,
                    probeTimeout,
                    launchedProcessId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (probeResult.IsReady)
            {
                return DaemonStartResult.Started(session);
            }

            return await CreateFailureWithCompensationAsync(
                    unityProject,
                    launchedProcessId,
                    launchedProcessStartedAtUtc,
                    expectedIssuedAtUtc,
                    probeResult.Error!,
                    "Daemon startup readiness probe failed",
                    "ProbeError")
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                    unityProject,
                    CreateTerminationTarget(launchedProcessId, launchedProcessStartedAtUtc),
                    DaemonTimeouts.LaunchCompensationTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<DaemonStartResult> LaunchGuiAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        CancellationToken cancellationToken)
    {
        var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        var launchResult = await unityGuiEditorProcessLauncher.LaunchAsync(
                unityProject,
                unityLogPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!launchResult.IsSuccess)
        {
            return DaemonStartResult.Failure(launchResult.Error!);
        }

        var processId = launchResult.ProcessId!.Value;
        var processStartedAtUtc = launchResult.ProcessStartedAtUtc!.Value;
        if (!deadline.TryGetRemainingTimeout(out var waitTimeout))
        {
            return await CreateGuiEndpointNotRegisteredFailureWithCompensationAsync(
                    unityProject,
                    processId,
                    processStartedAtUtc,
                    unityLogPath,
                    ExecutionError.Timeout(
                        "Timed out before GUI daemon session registration wait could begin.",
                        ExecutionErrorCodes.IpcTimeout))
                .ConfigureAwait(false);
        }

        DaemonGuiStartupObservationResult waitResult;
        try
        {
            waitResult = await guiStartupObserver.WaitForStartupAsync(
                    unityProject,
                    processId,
                    processStartedAtUtc,
                    unityLogPath,
                    waitTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                    unityProject,
                    CreateTerminationTarget(processId, processStartedAtUtc),
                    DaemonTimeouts.LaunchCompensationTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
        if (waitResult.IsSuccess)
        {
            return DaemonStartResult.Started(waitResult.Session!);
        }

        if (waitResult.IsBlocked)
        {
            return await CreateGuiStartupBlockedFailureAsync(
                    unityProject,
                    waitResult.Blocker!,
                    onStartupBlocked)
                .ConfigureAwait(false);
        }

        if (waitResult.Error!.Kind == ExecutionErrorKind.Timeout)
        {
            return await CreateGuiEndpointNotRegisteredFailureWithCompensationAsync(
                    unityProject,
                    processId,
                    processStartedAtUtc,
                    unityLogPath,
                    waitResult.Error)
                .ConfigureAwait(false);
        }

        return await CreateGuiStartupFailureWithCompensationAsync(
                unityProject,
                processId,
                processStartedAtUtc,
                waitResult.Error)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonStartResult> CreateFailureWithCompensationAsync (
        ResolvedUnityProjectContext unityProject,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        DateTimeOffset expectedIssuedAtUtc,
        ExecutionError primaryError,
        string primaryErrorMessagePrefix,
        string primaryErrorLabel)
    {
        var diagnosis = new DaemonDiagnosis(
            Reason: DaemonDiagnosisReasonValues.StartupFailed,
            Message: primaryError.Message,
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: false,
            UpdatedAtUtc: timeProvider.GetUtcNow(),
            ProcessId: processId,
            EditorInstancePath: null,
            SessionIssuedAtUtc: expectedIssuedAtUtc,
            ProcessStartedAtUtc: processStartedAtUtc);
        var diagnosisWriteResult = await daemonDiagnosisStore.WriteAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                diagnosis,
                CancellationToken.None)
            .ConfigureAwait(false);
        var compensationResult = await daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                unityProject,
                CreateTerminationTarget(processId, processStartedAtUtc),
                DaemonTimeouts.LaunchCompensationTimeout,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (!diagnosisWriteResult.IsSuccess && !compensationResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                $"{primaryErrorMessagePrefix}, diagnosis persistence failed, and cleanup failed. " +
                $"{primaryErrorLabel}={primaryError.Message} " +
                $"DiagnosisError={diagnosisWriteResult.Error!.Message} " +
                $"CleanupError={compensationResult.Error!.Message}"), diagnosis);
        }

        if (!diagnosisWriteResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                $"{primaryErrorMessagePrefix} and diagnosis persistence failed. " +
                $"{primaryErrorLabel}={primaryError.Message} " +
                $"DiagnosisError={diagnosisWriteResult.Error!.Message}"), diagnosis);
        }

        if (!compensationResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                $"{primaryErrorMessagePrefix} and cleanup failed. " +
                $"{primaryErrorLabel}={primaryError.Message} " +
                $"CleanupError={compensationResult.Error!.Message}"), diagnosis);
        }

        return DaemonStartResult.Failure(primaryError, diagnosis);
    }

    private static DaemonProcessTerminationTarget? CreateTerminationTarget (
        int? processId,
        DateTimeOffset? processStartedAtUtc)
    {
        if (processId is not int launchedProcessId)
        {
            return null;
        }

        return new DaemonProcessTerminationTarget(
            ProcessId: launchedProcessId,
            ProcessStartedAtUtc: processStartedAtUtc);
    }

    private async ValueTask<DaemonStartResult> CreateGuiEndpointNotRegisteredFailureAsync (
        ResolvedUnityProjectContext unityProject,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        string? unityLogPath,
        ExecutionError waitError)
    {
        return await DaemonGuiEndpointNotRegisteredFailureFactory.CreateFailureAsync(
                unityProject,
                daemonDiagnosisStore,
                timeProvider,
                "GUI Editor",
                UnityEditorInstanceMarkerPath.Resolve(unityProject.UnityProjectRoot),
                processId,
                waitError,
                processStartedAtUtc,
                unityLogPath)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonStartResult> CreateGuiEndpointNotRegisteredFailureWithCompensationAsync (
        ResolvedUnityProjectContext unityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        string unityLogPath,
        ExecutionError waitError)
    {
        var startResult = await CreateGuiEndpointNotRegisteredFailureAsync(
                unityProject,
                processId,
                processStartedAtUtc,
                unityLogPath,
                waitError)
            .ConfigureAwait(false);
        var compensationResult = await daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                unityProject,
                CreateTerminationTarget(processId, processStartedAtUtc),
                DaemonTimeouts.LaunchCompensationTimeout,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (compensationResult.IsSuccess)
        {
            return startResult;
        }

        return DaemonStartResult.Failure(
            CreateAugmentedPrimaryError(
                startResult.Error!,
                "GUI endpoint registration failed and cleanup failed. " +
                $"RegistrationError={startResult.Error!.Message} " +
                $"CleanupError={compensationResult.Error!.Message}"),
            startResult.Diagnosis);
    }

    private async ValueTask<DaemonStartResult> CreateGuiStartupFailureWithCompensationAsync (
        ResolvedUnityProjectContext unityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        ExecutionError startupError)
    {
        var compensationResult = await daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                unityProject,
                CreateTerminationTarget(processId, processStartedAtUtc),
                DaemonTimeouts.LaunchCompensationTimeout,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (compensationResult.IsSuccess)
        {
            return DaemonStartResult.Failure(startupError);
        }

        return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
            startupError,
            "GUI startup failed and cleanup failed. " +
            $"StartupError={startupError.Message} " +
            $"CleanupError={compensationResult.Error!.Message}"));
    }

    private async ValueTask<DaemonStartResult> CreateGuiStartupBlockedFailureAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonGuiStartupBlocker blocker,
        DaemonStartupBlockedProcessPolicy onStartupBlocked)
    {
        ArgumentNullException.ThrowIfNull(blocker);

        var primaryError = ExecutionError.InternalError(blocker.Message, DaemonErrorCodes.DaemonStartupBlocked);
        var updatedAtUtc = timeProvider.GetUtcNow();
        var diagnosis = new DaemonDiagnosis(
            Reason: blocker.Reason,
            Message: blocker.Message,
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: updatedAtUtc,
            ProcessId: blocker.ProcessId,
            EditorInstancePath: UnityEditorInstanceMarkerPath.Resolve(unityProject.UnityProjectRoot),
            SessionIssuedAtUtc: updatedAtUtc,
            ProcessStartedAtUtc: blocker.ProcessStartedAtUtc,
            UnityLogPath: blocker.UnityLogPath,
            StartupPhase: blocker.StartupPhase,
            ActionRequired: blocker.ActionRequired,
            PrimaryDiagnostic: blocker.PrimaryDiagnostic);
        var diagnosisWriteResult = await daemonDiagnosisStore.WriteAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                diagnosis,
                CancellationToken.None)
            .ConfigureAwait(false);
        var processPolicyResult = await ApplyGuiStartupBlockedProcessPolicyAsync(
                unityProject,
                blocker,
                onStartupBlocked,
                CancellationToken.None)
            .ConfigureAwait(false);
        var startup = CreateGuiStartupBlockedObservation(
            blocker,
            processPolicyResult.ProcessAction);
        var cleanupResult = processPolicyResult.CleanupResult;
        if (!diagnosisWriteResult.IsSuccess && cleanupResult is { IsSuccess: false })
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                "GUI startup is blocked, diagnosis persistence failed, and cleanup failed. " +
                $"StartupError={primaryError.Message} " +
                $"DiagnosisError={diagnosisWriteResult.Error!.Message} " +
                $"CleanupError={cleanupResult.Error!.Message}"), diagnosis, startup);
        }

        if (!diagnosisWriteResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                "GUI startup is blocked and diagnosis persistence failed. " +
                $"StartupError={primaryError.Message} " +
                $"DiagnosisError={diagnosisWriteResult.Error!.Message}"), diagnosis, startup);
        }

        if (cleanupResult is { IsSuccess: false })
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                "GUI startup is blocked and cleanup failed. " +
                $"StartupError={primaryError.Message} " +
                $"CleanupError={cleanupResult.Error!.Message}"), diagnosis, startup);
        }

        return DaemonStartResult.Failure(primaryError, diagnosis, startup);
    }

    private async ValueTask<GuiStartupBlockedProcessPolicyResult> ApplyGuiStartupBlockedProcessPolicyAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonGuiStartupBlocker blocker,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (IsProcessExitBlocker(blocker))
        {
            var cleanupResult = await daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                    unityProject,
                    target: null,
                    DaemonTimeouts.LaunchCompensationTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            return new GuiStartupBlockedProcessPolicyResult(
                cleanupResult,
                DaemonStartupProcessActionValues.None);
        }

        if (onStartupBlocked != DaemonStartupBlockedProcessPolicy.Terminate)
        {
            // NOTE: GUI blockers are kept by default so users can resolve Safe Mode, modal, or project errors in Unity.
            return new GuiStartupBlockedProcessPolicyResult(
                CleanupResult: null,
                ProcessAction: DaemonStartupProcessActionValues.Kept);
        }

        var terminationResult = await daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                unityProject,
                CreateTerminationTarget(blocker.ProcessId, blocker.ProcessStartedAtUtc),
                DaemonTimeouts.LaunchCompensationTimeout,
                cancellationToken)
            .ConfigureAwait(false);

        return new GuiStartupBlockedProcessPolicyResult(
            terminationResult,
            terminationResult.IsSuccess
                ? DaemonStartupProcessActionValues.Terminated
                : DaemonStartupProcessActionValues.Unknown);
    }

    private static bool IsProcessExitBlocker (DaemonGuiStartupBlocker blocker)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        return string.Equals(
            blocker.Reason,
            DaemonDiagnosisReasonValues.EditorExitedBeforeBootstrap,
            StringComparison.Ordinal);
    }

    private static DaemonStartupObservation CreateGuiStartupBlockedObservation (
        DaemonGuiStartupBlocker blocker,
        string processAction)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        ArgumentException.ThrowIfNullOrWhiteSpace(processAction);

        return new DaemonStartupObservation(
            StartupStatus: DaemonStartupStatusValues.Blocked,
            StartupBlockingReason: blocker.StartupBlockingReason,
            LaunchAttemptId: null,
            ProcessAction: processAction,
            RetryDisposition: blocker.RetryDisposition);
    }

    private sealed record GuiStartupBlockedProcessPolicyResult (
        DaemonSessionStoreOperationResult? CleanupResult,
        string ProcessAction);

    private static ExecutionError CreateAugmentedPrimaryError (
        ExecutionError primaryError,
        string message)
    {
        ArgumentNullException.ThrowIfNull(primaryError);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return primaryError.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => ExecutionError.InvalidArgument(message, primaryError.Code),
            ExecutionErrorKind.Timeout => ExecutionError.Timeout(message, primaryError.Code),
            ExecutionErrorKind.InternalError => ExecutionError.InternalError(message, primaryError.Code),
            _ => throw new ArgumentOutOfRangeException(nameof(primaryError), primaryError.Kind, "Unsupported execution error kind."),
        };
    }
}

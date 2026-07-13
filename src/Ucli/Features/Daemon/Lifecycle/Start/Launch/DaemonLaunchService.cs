using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Launch;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Launch;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
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

    private readonly IDaemonLaunchAttemptIdGenerator launchAttemptIdGenerator;

    private readonly IDaemonLaunchAttemptStore launchAttemptStore;

    private readonly DaemonCompensationOperationOwner compensationOperationOwner;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonLaunchService" /> class. </summary>
    /// <param name="daemonLaunchSessionService"> The daemon launch-session service dependency. </param>
    /// <param name="unityDaemonProcessLauncher"> The Unity daemon process-launcher dependency. </param>
    /// <param name="unityGuiEditorProcessLauncher"> The Unity GUI Editor process-launcher dependency. </param>
    /// <param name="startupReadinessProbe"> The daemon startup-readiness probe dependency. </param>
    /// <param name="guiStartupObserver"> The GUI startup observer dependency. </param>
    /// <param name="daemonLaunchCompensationService"> The daemon launch-compensation service dependency. </param>
    /// <param name="daemonDiagnosisStore"> The daemon diagnosis store dependency. </param>
    /// <param name="launchAttemptIdGenerator"> The launch-attempt identifier generator dependency. </param>
    /// <param name="launchAttemptStore"> The launch-attempt artifact store dependency. </param>
    /// <param name="compensationOperationOwner"> The owner for compensation that outlives a caller deadline. </param>
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
        IDaemonLaunchAttemptIdGenerator launchAttemptIdGenerator,
        IDaemonLaunchAttemptStore launchAttemptStore,
        DaemonCompensationOperationOwner compensationOperationOwner,
        TimeProvider timeProvider)
    {
        this.daemonLaunchSessionService = daemonLaunchSessionService ?? throw new ArgumentNullException(nameof(daemonLaunchSessionService));
        this.unityDaemonProcessLauncher = unityDaemonProcessLauncher ?? throw new ArgumentNullException(nameof(unityDaemonProcessLauncher));
        this.unityGuiEditorProcessLauncher = unityGuiEditorProcessLauncher ?? throw new ArgumentNullException(nameof(unityGuiEditorProcessLauncher));
        this.startupReadinessProbe = startupReadinessProbe ?? throw new ArgumentNullException(nameof(startupReadinessProbe));
        this.guiStartupObserver = guiStartupObserver ?? throw new ArgumentNullException(nameof(guiStartupObserver));
        this.daemonLaunchCompensationService = daemonLaunchCompensationService ?? throw new ArgumentNullException(nameof(daemonLaunchCompensationService));
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
        this.launchAttemptIdGenerator = launchAttemptIdGenerator ?? throw new ArgumentNullException(nameof(launchAttemptIdGenerator));
        this.launchAttemptStore = launchAttemptStore ?? throw new ArgumentNullException(nameof(launchAttemptStore));
        this.compensationOperationOwner = compensationOperationOwner ?? throw new ArgumentNullException(nameof(compensationOperationOwner));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Launches daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon startup timeout. </param>
    /// <param name="editorMode"> The requested daemon Editor mode. </param>
    /// <param name="onStartupBlocked"> The startup-blocked process policy requested by the caller. </param>
    /// <param name="progressObserver"> The optional observer for supervisor-internal start progress. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartResult> LaunchAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonStartProgressObserver? progressObserver = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var admissionError = await compensationOperationOwner.WaitForQuiescenceAsync(
                unityProject,
                deadline,
                cancellationToken,
                "Timed out waiting for prior daemon compensation to quiesce.")
            .ConfigureAwait(false);
        if (admissionError is not null)
        {
            return DaemonStartResult.Failure(admissionError);
        }

        var launchStartedAtUtc = timeProvider.GetUtcNow();
        var launchAttemptId = CreateUniqueLaunchAttemptId(unityProject, launchStartedAtUtc);

        return editorMode switch
        {
            DaemonEditorMode.Batchmode => await LaunchBatchmodeAsync(
                    unityProject,
                    editorMode,
                    deadline,
                    onStartupBlocked,
                    launchAttemptId,
                    launchStartedAtUtc,
                    progressObserver,
                    cancellationToken)
                .ConfigureAwait(false),
            DaemonEditorMode.Gui => await LaunchGuiAsync(
                    unityProject,
                    deadline,
                    onStartupBlocked,
                    launchAttemptId,
                    launchStartedAtUtc,
                    progressObserver,
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
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        string launchAttemptId,
        DateTimeOffset launchStartedAtUtc,
        IDaemonStartProgressObserver? progressObserver,
        CancellationToken cancellationToken)
    {
        var initializeSessionResult = await daemonLaunchSessionService.InitializeAsync(
                unityProject,
                editorMode,
                cancellationToken)
            .ConfigureAwait(false);
        if (!initializeSessionResult.IsSuccess)
        {
            return await CreateFailureWithLaunchAttemptAsync(
                    unityProject,
                    launchAttemptId,
                    launchStartedAtUtc,
                    processId: null,
                    processStartedAtUtc: null,
                    sessionIssuedAtUtc: launchStartedAtUtc,
                    unityLogPath: null,
                    editorMode: DaemonEditorMode.Batchmode,
                    initializeSessionResult.Error!,
                    startupStatus: DaemonStartupStatus.Failed,
                    startupBlockingReason: DaemonStartupBlockingReason.Unknown,
                    retryDisposition: DaemonStartupRetryDisposition.Unknown,
                    processAction: DaemonStartupProcessAction.None)
                .ConfigureAwait(false);
        }
        var session = initializeSessionResult.Session!;
        var launchedProcessId = default(int?);
        var launchedProcessStartedAtUtc = default(DateTimeOffset?);

        try
        {
            await EmitSessionRegisteredAsync(progressObserver, session, launchAttemptId, cancellationToken).ConfigureAwait(false);
            var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint);
            await EmitLaunchingAsync(
                    progressObserver,
                    launchAttemptId,
                    DaemonEditorMode.Batchmode,
                    DaemonSessionOwnerKind.Cli,
                    canShutdownProcess: true,
                    processId: null,
                    processStartedAtUtc: null,
                    cancellationToken)
                .ConfigureAwait(false);
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
                        session,
                        launchAttemptId,
                        launchStartedAtUtc,
                        DaemonEditorMode.Batchmode,
                        unityLogPath,
                        launchResult.Error!,
                        DaemonStartupStatus.Failed,
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
                        session,
                        launchAttemptId,
                        launchStartedAtUtc,
                        DaemonEditorMode.Batchmode,
                        unityLogPath,
                        updateProcessIdResult.Error!,
                        DaemonStartupStatus.Failed,
                        "Daemon session update failed",
                        "SessionError")
                    .ConfigureAwait(false);
            }

            session = updateProcessIdResult.Session!;
            await EmitWaitingForEndpointAsync(
                    progressObserver,
                    launchAttemptId,
                    session.EditorMode,
                    session.OwnerKind,
                    session.CanShutdownProcess,
                    launchedProcessId,
                    launchedProcessStartedAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!deadline.TryGetRemainingTimeout(out var probeTimeout))
            {
                return await CreateFailureWithCompensationAsync(
                        unityProject,
                        launchedProcessId,
                        launchedProcessStartedAtUtc,
                        session,
                        launchAttemptId,
                        launchStartedAtUtc,
                        DaemonEditorMode.Batchmode,
                        unityLogPath,
                        ExecutionError.Timeout("Timed out before daemon startup readiness probe could begin."),
                        DaemonStartupStatus.Timeout,
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
                await EmitEndpointReadyAsync(
                        progressObserver,
                        session,
                        launchAttemptId,
                        probeResult.LifecycleObservation,
                        emitSessionRegistered: false,
                        cancellationToken)
                    .ConfigureAwait(false);
                return DaemonStartResult.Started(session, probeResult.LifecycleObservation);
            }

            if (probeResult.FailureClassification is not null)
            {
                await EmitBatchmodeBlockerDetectedAsync(
                        progressObserver,
                        probeResult.FailureClassification,
                        probeResult.Error!,
                        launchAttemptId,
                        launchedProcessId,
                        launchedProcessStartedAtUtc,
                        cancellationToken)
                    .ConfigureAwait(false);
                return await CreateClassifiedBatchmodeStartupBlockedFailureAsync(
                        unityProject,
                        probeResult.FailureClassification,
                        probeResult.Error!,
                        onStartupBlocked,
                        launchedProcessId,
                        launchedProcessStartedAtUtc,
                        session,
                        launchAttemptId,
                        launchStartedAtUtc,
                        unityLogPath)
                    .ConfigureAwait(false);
            }

            return await CreateFailureWithCompensationAsync(
                    unityProject,
                    launchedProcessId,
                    launchedProcessStartedAtUtc,
                    session,
                    launchAttemptId,
                    launchStartedAtUtc,
                    DaemonEditorMode.Batchmode,
                    unityLogPath,
                    probeResult.Error!,
                    probeResult.Error!.Kind == ExecutionErrorKind.Timeout
                        ? DaemonStartupStatus.Timeout
                        : DaemonStartupStatus.Failed,
                    "Daemon startup readiness probe failed",
                    "ProbeError")
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            await RunLaunchCompensationBestEffortAsync(
                    unityProject,
                    session,
                    CreateTerminationTarget(launchedProcessId, launchedProcessStartedAtUtc))
                .ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<DaemonStartResult> LaunchGuiAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        string launchAttemptId,
        DateTimeOffset launchStartedAtUtc,
        IDaemonStartProgressObserver? progressObserver,
        CancellationToken cancellationToken)
    {
        var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        await EmitLaunchingAsync(
                progressObserver,
                launchAttemptId,
                DaemonEditorMode.Gui,
                DaemonSessionOwnerKind.Cli,
                canShutdownProcess: true,
                processId: null,
                processStartedAtUtc: null,
                cancellationToken)
            .ConfigureAwait(false);
        var launchResult = await unityGuiEditorProcessLauncher.LaunchAsync(
                unityProject,
                unityLogPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!launchResult.IsSuccess)
        {
            return await CreateFailureWithLaunchAttemptAsync(
                    unityProject,
                    launchAttemptId,
                    launchStartedAtUtc,
                    launchResult.ProcessId,
                    launchResult.ProcessStartedAtUtc,
                    launchStartedAtUtc,
                    unityLogPath,
                    DaemonEditorMode.Gui,
                    launchResult.Error!,
                    startupStatus: DaemonStartupStatus.Failed,
                    startupBlockingReason: DaemonStartupBlockingReason.Unknown,
                    retryDisposition: DaemonStartupRetryDisposition.Unknown,
                    processAction: DaemonStartupProcessAction.None)
                .ConfigureAwait(false);
        }

        var processId = launchResult.ProcessId!.Value;
        var processStartedAtUtc = launchResult.ProcessStartedAtUtc!.Value;
        try
        {
            await EmitWaitingForEndpointAsync(
                progressObserver,
                launchAttemptId,
                DaemonEditorMode.Gui,
                DaemonSessionOwnerKind.Cli,
                canShutdownProcess: true,
                processId,
                processStartedAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
            if (!deadline.TryGetRemainingTimeout(out var waitTimeout))
            {
                return await CreateGuiEndpointNotRegisteredFailureWithCompensationAsync(
                        unityProject,
                        processId,
                        processStartedAtUtc,
                        unityLogPath,
                        launchAttemptId,
                        launchStartedAtUtc,
                        onStartupBlocked,
                        ExecutionError.Timeout(
                            "Timed out before GUI daemon session registration wait could begin.",
                            ExecutionErrorCodes.IpcTimeout))
                    .ConfigureAwait(false);
            }

            var waitResult = await guiStartupObserver.WaitForStartupAsync(
                    unityProject,
                    processId,
                    processStartedAtUtc,
                    unityLogPath,
                    waitTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (waitResult.IsSuccess)
            {
                await EmitEndpointReadyAsync(
                    progressObserver,
                    waitResult.Session,
                    launchAttemptId,
                    waitResult.LifecycleObservation,
                    emitSessionRegistered: true,
                    cancellationToken)
                .ConfigureAwait(false);
                return DaemonStartResult.Started(waitResult.Session, waitResult.LifecycleObservation);
            }

            if (waitResult.IsBlocked)
            {
                await EmitGuiBlockerDetectedAsync(
                        progressObserver,
                        waitResult.BlockerObservation,
                        launchAttemptId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return await CreateGuiStartupBlockedFailureAsync(
                        unityProject,
                        waitResult.BlockerObservation,
                        launchAttemptId,
                        launchStartedAtUtc,
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
                    launchAttemptId,
                    launchStartedAtUtc,
                    onStartupBlocked,
                    waitResult.Error)
                .ConfigureAwait(false);
            }

            return await CreateGuiStartupFailureWithCompensationAsync(
                unityProject,
                processId,
                processStartedAtUtc,
                unityLogPath,
                launchAttemptId,
                launchStartedAtUtc,
                waitResult.Error)
            .ConfigureAwait(false);
        }
        catch (Exception)
        {
            await RunLaunchCompensationBestEffortAsync(
                    unityProject,
                    expectedSession: null,
                    target: CreateTerminationTarget(processId, processStartedAtUtc))
                .ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<DaemonStartResult> CreateFailureWithCompensationAsync (
        ResolvedUnityProjectContext unityProject,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        DaemonSession expectedSession,
        string launchAttemptId,
        DateTimeOffset launchStartedAtUtc,
        DaemonEditorMode editorMode,
        string? unityLogPath,
        ExecutionError primaryError,
        DaemonStartupStatus startupStatus,
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
            SessionIssuedAtUtc: expectedSession.IssuedAtUtc,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath);
        var compensationResult = await RunLaunchCompensationAsync(
                unityProject,
                expectedSession,
                CreateTerminationTarget(processId, processStartedAtUtc),
                CancellationToken.None)
            .ConfigureAwait(false);
        var diagnosisWriteResult = await WriteDiagnosisAsync(unityProject, diagnosis).ConfigureAwait(false);
        var processAction = ResolveCompensatedProcessAction(processId, compensationResult);
        var startup = CreateStartupFailureObservation(
            startupStatus,
            DaemonStartupBlockingReason.Unknown,
            launchAttemptId,
            processAction,
            DaemonStartupRetryDisposition.Unknown,
            editorMode,
            DaemonSessionOwnerKind.Cli,
            processId is not null,
            processId,
            processStartedAtUtc,
            launchStartedAtUtc,
            diagnosis.UpdatedAtUtc,
            CreateLaunchAttemptArtifactPath(unityProject, launchAttemptId));
        var launchAttemptWriteResult = await WriteLaunchAttemptAsync(
                unityProject,
                launchAttemptId,
                launchStartedAtUtc,
                diagnosis.UpdatedAtUtc,
                startupStatus,
                DaemonStartupBlockingReason.Unknown,
                DaemonStartupRetryDisposition.Unknown,
                processAction,
                editorMode,
                processId,
                processStartedAtUtc,
                unityLogPath,
                diagnosis)
            .ConfigureAwait(false);
        if (!diagnosisWriteResult.IsSuccess && !compensationResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                $"{primaryErrorMessagePrefix}, diagnosis persistence failed, and cleanup failed. " +
                $"{primaryErrorLabel}={primaryError.Message} " +
                $"DiagnosisError={diagnosisWriteResult.Error!.Message} " +
                $"CleanupError={compensationResult.Error!.Message}"), diagnosis, startup);
        }

        if (!launchAttemptWriteResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                $"{primaryErrorMessagePrefix} and launch-attempt artifact persistence failed. " +
                $"{primaryErrorLabel}={primaryError.Message} " +
                $"ArtifactError={launchAttemptWriteResult.Error!.Message}"), diagnosis, startup);
        }

        if (!diagnosisWriteResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                $"{primaryErrorMessagePrefix} and diagnosis persistence failed. " +
                $"{primaryErrorLabel}={primaryError.Message} " +
                $"DiagnosisError={diagnosisWriteResult.Error!.Message}"), diagnosis, startup);
        }

        if (!compensationResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                $"{primaryErrorMessagePrefix} and cleanup failed. " +
                $"{primaryErrorLabel}={primaryError.Message} " +
                $"CleanupError={compensationResult.Error!.Message}"), diagnosis, startup);
        }

        return DaemonStartResult.Failure(primaryError, diagnosis, startup);
    }

    private async ValueTask<DaemonStartResult> CreateFailureWithLaunchAttemptAsync (
        ResolvedUnityProjectContext unityProject,
        string launchAttemptId,
        DateTimeOffset launchStartedAtUtc,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        DateTimeOffset sessionIssuedAtUtc,
        string? unityLogPath,
        DaemonEditorMode? editorMode,
        ExecutionError primaryError,
        DaemonStartupStatus startupStatus,
        DaemonStartupBlockingReason? startupBlockingReason,
        DaemonStartupRetryDisposition retryDisposition,
        DaemonStartupProcessAction processAction)
    {
        var updatedAtUtc = timeProvider.GetUtcNow();
        var diagnosis = new DaemonDiagnosis(
            Reason: DaemonDiagnosisReasonValues.StartupFailed,
            Message: primaryError.Message,
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: false,
            UpdatedAtUtc: updatedAtUtc,
            ProcessId: processId,
            EditorInstancePath: null,
            SessionIssuedAtUtc: sessionIssuedAtUtc,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath);
        var launchAttemptWriteResult = await WriteLaunchAttemptAsync(
                unityProject,
                launchAttemptId,
                launchStartedAtUtc,
                updatedAtUtc,
                startupStatus,
                startupBlockingReason,
                retryDisposition,
                processAction,
                editorMode,
                processId,
                processStartedAtUtc,
                unityLogPath,
                diagnosis)
            .ConfigureAwait(false);
        var startup = CreateStartupFailureObservation(
            startupStatus,
            startupBlockingReason,
            launchAttemptId,
            processAction,
            retryDisposition,
            editorMode,
            DaemonSessionOwnerKind.Cli,
            processId is not null,
            processId,
            processStartedAtUtc,
            launchStartedAtUtc,
            updatedAtUtc,
            CreateLaunchAttemptArtifactPath(unityProject, launchAttemptId));
        if (!launchAttemptWriteResult.IsSuccess)
        {
            return DaemonStartResult.Failure(
                CreateAugmentedPrimaryError(
                    primaryError,
                    "Daemon startup failed and launch-attempt artifact persistence failed. " +
                    $"StartupError={primaryError.Message} " +
                    $"ArtifactError={launchAttemptWriteResult.Error!.Message}"),
                diagnosis,
                startup);
        }

        return DaemonStartResult.Failure(primaryError, diagnosis, startup);
    }

    private async ValueTask<DaemonLaunchAttemptStoreOperationResult> WriteLaunchAttemptAsync (
        ResolvedUnityProjectContext unityProject,
        string launchAttemptId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset updatedAtUtc,
        DaemonStartupStatus startupStatus,
        DaemonStartupBlockingReason? startupBlockingReason,
        DaemonStartupRetryDisposition retryDisposition,
        DaemonStartupProcessAction processAction,
        DaemonEditorMode? editorMode,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        string? unityLogPath,
        DaemonDiagnosis diagnosis,
        bool pruneAfterWrite = true)
    {
        var artifactPath = UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            launchAttemptId);
        var launchAttempt = new DaemonLaunchAttempt(
            LaunchAttemptId: launchAttemptId,
            StartedAtUtc: startedAtUtc,
            UpdatedAtUtc: updatedAtUtc,
            StartupStatus: startupStatus,
            StartupBlockingReason: startupBlockingReason,
            RetryDisposition: retryDisposition,
            ProcessAction: processAction,
            EditorMode: editorMode,
            ProcessId: processId,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath,
            ArtifactPath: artifactPath,
            Diagnosis: diagnosis);
        var persistenceDeadline = ExecutionDeadline.Start(
            DaemonTimeouts.SupplementalPersistenceTimeout,
            timeProvider);
        var executionResult = await compensationOperationOwner.ExecuteAsync(
                unityProject,
                DaemonOperationLane.SupplementalPersistence,
                persistenceDeadline,
                CancellationToken.None,
                "Timed out before launch-attempt persistence could begin.",
                "Timed out while persisting the launch-attempt artifact.",
                (_, ownedCancellationToken) => WriteLaunchAttemptCoreAsync(
                    unityProject,
                    launchAttempt,
                    pruneAfterWrite,
                    ownedCancellationToken))
            .ConfigureAwait(false);
        return executionResult.IsSuccess
            ? executionResult.Value!
            : DaemonLaunchAttemptStoreOperationResult.Failure(executionResult.Error!);
    }

    private async ValueTask<DaemonLaunchAttemptStoreOperationResult> WriteLaunchAttemptCoreAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonLaunchAttempt launchAttempt,
        bool pruneAfterWrite,
        CancellationToken cancellationToken)
    {
        var writeResult = await launchAttemptStore.WriteFailureAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                launchAttempt,
                cancellationToken)
            .ConfigureAwait(false);
        if (!writeResult.IsSuccess)
        {
            return writeResult;
        }

        if (!pruneAfterWrite)
        {
            return DaemonLaunchAttemptStoreOperationResult.Success();
        }

        return await launchAttemptStore.PruneAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                keepCount: 20,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonDiagnosisStoreOperationResult> WriteDiagnosisAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonDiagnosis diagnosis)
    {
        var persistenceDeadline = ExecutionDeadline.Start(
            DaemonTimeouts.SupplementalPersistenceTimeout,
            timeProvider);
        var executionResult = await compensationOperationOwner.ExecuteAsync(
                unityProject,
                DaemonOperationLane.SupplementalPersistence,
                persistenceDeadline,
                CancellationToken.None,
                "Timed out before daemon diagnosis persistence could begin.",
                "Timed out while persisting daemon diagnosis.",
                (_, ownedCancellationToken) => daemonDiagnosisStore.WriteAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    diagnosis,
                    ownedCancellationToken))
            .ConfigureAwait(false);
        return executionResult.IsSuccess
            ? executionResult.Value!
            : DaemonDiagnosisStoreOperationResult.Failure(executionResult.Error!);
    }

    private async ValueTask<DaemonSessionStoreOperationResult> RunLaunchCompensationAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession? expectedSession,
        DaemonProcessTerminationTarget? target,
        CancellationToken cancellationToken)
    {
        var compensationDeadline = ExecutionDeadline.Start(
            DaemonTimeouts.LaunchCompensationTimeout,
            timeProvider);
        var executionResult = await compensationOperationOwner.ExecuteAsync(
                unityProject,
                DaemonOperationLane.LifecycleCompensation,
                compensationDeadline,
                cancellationToken,
                "Timed out waiting for prior daemon compensation to quiesce.",
                "Timed out before failed daemon launch compensation could complete.",
                (remainingTimeout, ownedCancellationToken) =>
                    daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                        unityProject,
                        expectedSession,
                        target,
                        remainingTimeout,
                        ownedCancellationToken))
            .ConfigureAwait(false);
        return executionResult.IsSuccess
            ? executionResult.Value!
            : DaemonSessionStoreOperationResult.Failure(executionResult.Error!);
    }

    private async ValueTask RunLaunchCompensationBestEffortAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession? expectedSession,
        DaemonProcessTerminationTarget? target)
    {
        try
        {
            _ = await RunLaunchCompensationAsync(
                    unityProject,
                    expectedSession,
                    target,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The failure that entered this compensation boundary remains the primary exception.
        }
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

    private static string CreateLaunchAttemptArtifactPath (
        ResolvedUnityProjectContext unityProject,
        string launchAttemptId)
    {
        return UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            launchAttemptId);
    }

    private static DaemonStartupObservation CreateStartupFailureObservation (
        DaemonStartupStatus startupStatus,
        DaemonStartupBlockingReason? startupBlockingReason,
        string launchAttemptId,
        DaemonStartupProcessAction processAction,
        DaemonStartupRetryDisposition retryDisposition,
        DaemonEditorMode? editorMode,
        DaemonSessionOwnerKind? ownerKind,
        bool? canShutdownProcess,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        DateTimeOffset launchStartedAtUtc,
        DateTimeOffset updatedAtUtc,
        string? artifactPath)
    {
        return new DaemonStartupObservation(
            StartupStatus: startupStatus,
            StartupBlockingReason: startupBlockingReason,
            LaunchAttemptId: launchAttemptId,
            ProcessAction: processAction,
            RetryDisposition: retryDisposition,
            EditorMode: editorMode,
            OwnerKind: ownerKind,
            CanShutdownProcess: canShutdownProcess,
            ProcessId: processId,
            StartedAtUtc: processStartedAtUtc,
            ElapsedMilliseconds: checked((int)Math.Max(0, (updatedAtUtc - launchStartedAtUtc).TotalMilliseconds)),
            ArtifactPath: artifactPath);
    }

    private async ValueTask<DaemonStartResult> CreateClassifiedBatchmodeStartupBlockedFailureAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonStartupFailureClassification classification,
        ExecutionError primaryError,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        DaemonSession expectedSession,
        string launchAttemptId,
        DateTimeOffset launchStartedAtUtc,
        string? unityLogPath)
    {
        var updatedAtUtc = timeProvider.GetUtcNow();
        var diagnosis = new DaemonDiagnosis(
            Reason: classification.Reason,
            Message: primaryError.Message,
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: updatedAtUtc,
            ProcessId: processId,
            EditorInstancePath: null,
            SessionIssuedAtUtc: expectedSession.IssuedAtUtc,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath,
            StartupPhase: classification.StartupPhase,
            ActionRequired: classification.ActionRequired,
            PrimaryDiagnostic: classification.PrimaryDiagnostic);
        var policyResolution = DaemonStartupBlockedProcessPolicyResolver.Resolve(
            onStartupBlocked,
            DaemonEditorMode.Batchmode,
            DaemonSessionOwnerKind.Cli,
            canShutdownProcess: true,
            processId);
        // Mandatory process compensation starts before supplemental persistence. A blocked
        // diagnosis or launch-attempt store must never leave the launched process running.
        var policyResult = await ApplyBatchmodeStartupBlockedProcessPolicyAsync(
                unityProject,
                policyResolution,
                expectedSession,
                processId,
                processStartedAtUtc,
                CancellationToken.None)
            .ConfigureAwait(false);
        var diagnosisWriteResult = await WriteDiagnosisAsync(unityProject, diagnosis).ConfigureAwait(false);
        var launchAttemptWriteResult = await WriteLaunchAttemptAsync(
                unityProject,
                launchAttemptId,
                launchStartedAtUtc,
                updatedAtUtc,
                DaemonStartupStatus.Blocked,
                classification.StartupBlockingReason,
                classification.RetryDisposition,
                policyResult.ProcessAction,
                DaemonEditorMode.Batchmode,
                processId,
                processStartedAtUtc,
                unityLogPath,
                diagnosis)
            .ConfigureAwait(false);

        var startup = CreateStartupFailureObservation(
            DaemonStartupStatus.Blocked,
            classification.StartupBlockingReason,
            launchAttemptId,
            policyResult.ProcessAction,
            classification.RetryDisposition,
            DaemonEditorMode.Batchmode,
            DaemonSessionOwnerKind.Cli,
            processId is not null,
            processId,
            processStartedAtUtc,
            launchStartedAtUtc,
            updatedAtUtc,
            CreateLaunchAttemptArtifactPath(unityProject, launchAttemptId));
        if (!diagnosisWriteResult.IsSuccess && !launchAttemptWriteResult.IsSuccess && policyResult.CleanupResult is { IsSuccess: false })
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                "Batchmode startup is blocked, diagnosis persistence failed, launch-attempt artifact persistence failed, and cleanup failed. " +
                $"StartupError={primaryError.Message} " +
                $"DiagnosisError={diagnosisWriteResult.Error!.Message} " +
                $"ArtifactError={launchAttemptWriteResult.Error!.Message} " +
                $"CleanupError={policyResult.CleanupResult.Error!.Message}"), diagnosis, startup);
        }

        if (!diagnosisWriteResult.IsSuccess && policyResult.CleanupResult is { IsSuccess: false })
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                "Batchmode startup is blocked, diagnosis persistence failed, and cleanup failed. " +
                $"StartupError={primaryError.Message} " +
                $"DiagnosisError={diagnosisWriteResult.Error!.Message} " +
                $"CleanupError={policyResult.CleanupResult.Error!.Message}"), diagnosis, startup);
        }

        if (!launchAttemptWriteResult.IsSuccess && policyResult.CleanupResult is { IsSuccess: false })
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                "Batchmode startup is blocked, launch-attempt artifact persistence failed, and cleanup failed. " +
                $"StartupError={primaryError.Message} " +
                $"ArtifactError={launchAttemptWriteResult.Error!.Message} " +
                $"CleanupError={policyResult.CleanupResult.Error!.Message}"), diagnosis, startup);
        }

        if (!launchAttemptWriteResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                "Batchmode startup is blocked and launch-attempt artifact persistence failed. " +
                $"StartupError={primaryError.Message} " +
                $"ArtifactError={launchAttemptWriteResult.Error!.Message}"), diagnosis, startup);
        }

        if (!diagnosisWriteResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                "Batchmode startup is blocked and diagnosis persistence failed. " +
                $"StartupError={primaryError.Message} " +
                $"DiagnosisError={diagnosisWriteResult.Error!.Message}"), diagnosis, startup);
        }

        if (policyResult.CleanupResult is { IsSuccess: false })
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                "Batchmode startup is blocked and cleanup failed. " +
                $"StartupError={primaryError.Message} " +
                $"CleanupError={policyResult.CleanupResult.Error!.Message}"), diagnosis, startup);
        }

        return DaemonStartResult.Failure(primaryError, diagnosis, startup);
    }

    private async ValueTask<StartupBlockedProcessPolicyResult> ApplyBatchmodeStartupBlockedProcessPolicyAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonStartupBlockedProcessPolicyResolution policyResolution,
        DaemonSession expectedSession,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!policyResolution.ShouldTerminateProcess)
        {
            return new StartupBlockedProcessPolicyResult(
                CleanupResult: null,
                ProcessAction: policyResolution.ProcessActionWhenNotTerminated);
        }

        var cleanupResult = await RunLaunchCompensationAsync(
                unityProject,
                expectedSession,
                CreateTerminationTarget(processId, processStartedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);

        return new StartupBlockedProcessPolicyResult(
            cleanupResult,
            ResolveCompensatedProcessAction(processId, cleanupResult));
    }

    private static DaemonStartupProcessAction ResolveCompensatedProcessAction (
        int? processId,
        DaemonSessionStoreOperationResult compensationResult)
    {
        ArgumentNullException.ThrowIfNull(compensationResult);
        if (processId is null)
        {
            return DaemonStartupProcessAction.None;
        }

        return compensationResult.IsSuccess
            ? DaemonStartupProcessAction.Terminated
            : DaemonStartupProcessAction.Unknown;
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
                compensationOperationOwner,
                daemonDiagnosisStore,
                timeProvider,
                "GUI Editor",
                UnityEditorInstanceMarkerPath.Resolve(unityProject.UnityProjectRoot),
                processId,
                waitError,
                processStartedAtUtc,
                unityLogPath,
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonStartResult> CreateGuiEndpointNotRegisteredFailureWithCompensationAsync (
        ResolvedUnityProjectContext unityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        string unityLogPath,
        string launchAttemptId,
        DateTimeOffset launchStartedAtUtc,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        ExecutionError waitError)
    {
        var policyResolution = DaemonStartupBlockedProcessPolicyResolver.Resolve(
            onStartupBlocked,
            DaemonEditorMode.Gui,
            DaemonSessionOwnerKind.Cli,
            canShutdownProcess: true,
            processId);
        var compensationResult = policyResolution.ShouldTerminateProcess
            ? await RunLaunchCompensationAsync(
                    unityProject,
                    expectedSession: null,
                    target: CreateTerminationTarget(processId, processStartedAtUtc),
                    cancellationToken: CancellationToken.None)
                .ConfigureAwait(false)
            : null;
        var startResult = await CreateGuiEndpointNotRegisteredFailureAsync(
                unityProject,
                processId,
                processStartedAtUtc,
                unityLogPath,
                waitError)
            .ConfigureAwait(false);
        var processAction = compensationResult is null
            ? policyResolution.ProcessActionWhenNotTerminated
            : ResolveCompensatedProcessAction(processId, compensationResult);
        var updatedAtUtc = timeProvider.GetUtcNow();
        var startup = CreateStartupFailureObservation(
            DaemonStartupStatus.Timeout,
            DaemonStartupBlockingReason.EndpointNotRegistered,
            launchAttemptId,
            processAction,
            DaemonStartupRetryDisposition.WaitThenRetry,
            DaemonEditorMode.Gui,
            DaemonSessionOwnerKind.Cli,
            true,
            processId,
            processStartedAtUtc,
            launchStartedAtUtc,
            updatedAtUtc,
            CreateLaunchAttemptArtifactPath(unityProject, launchAttemptId));
        var launchAttemptWriteResult = await WriteLaunchAttemptAsync(
                unityProject,
                launchAttemptId,
                launchStartedAtUtc,
                updatedAtUtc,
                startup.StartupStatus,
                startup.StartupBlockingReason!,
                startup.RetryDisposition,
                startup.ProcessAction,
                DaemonEditorMode.Gui,
                processId,
                processStartedAtUtc,
                unityLogPath,
                startResult.Diagnosis!)
            .ConfigureAwait(false);
        if (!launchAttemptWriteResult.IsSuccess)
        {
            return DaemonStartResult.Failure(
                CreateAugmentedPrimaryError(
                    startResult.Error!,
                    "GUI endpoint registration failed and launch-attempt artifact persistence failed. " +
                    $"RegistrationError={startResult.Error!.Message} " +
                    $"ArtifactError={launchAttemptWriteResult.Error!.Message}"),
                startResult.Diagnosis,
                startup);
        }

        if (compensationResult is null || compensationResult.IsSuccess)
        {
            return DaemonStartResult.Failure(startResult.Error!, startResult.Diagnosis, startup);
        }

        return DaemonStartResult.Failure(
            CreateAugmentedPrimaryError(
                startResult.Error!,
                "GUI endpoint registration failed and cleanup failed. " +
                $"RegistrationError={startResult.Error!.Message} " +
                $"CleanupError={compensationResult.Error!.Message}"),
            startResult.Diagnosis,
            startup);
    }

    private async ValueTask<DaemonStartResult> CreateGuiStartupFailureWithCompensationAsync (
        ResolvedUnityProjectContext unityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        string unityLogPath,
        string launchAttemptId,
        DateTimeOffset launchStartedAtUtc,
        ExecutionError startupError)
    {
        var compensationResult = await RunLaunchCompensationAsync(
                unityProject,
                expectedSession: null,
                target: CreateTerminationTarget(processId, processStartedAtUtc),
                cancellationToken: CancellationToken.None)
            .ConfigureAwait(false);
        var processAction = ResolveCompensatedProcessAction(processId, compensationResult);
        var startResult = await CreateFailureWithLaunchAttemptAsync(
                unityProject,
                launchAttemptId,
                launchStartedAtUtc,
                processId,
                processStartedAtUtc,
                launchStartedAtUtc,
                unityLogPath,
                DaemonEditorMode.Gui,
                startupError,
                DaemonStartupStatus.Failed,
                DaemonStartupBlockingReason.Unknown,
                DaemonStartupRetryDisposition.Unknown,
                processAction)
            .ConfigureAwait(false);
        if (compensationResult.IsSuccess)
        {
            return startResult;
        }

        return DaemonStartResult.Failure(
            CreateAugmentedPrimaryError(
                startResult.Error!,
                "GUI startup failed and cleanup failed. " +
                $"StartupError={startResult.Error!.Message} " +
                $"CleanupError={compensationResult.Error!.Message}"),
            startResult.Diagnosis,
            startResult.Startup);
    }

    private async ValueTask<DaemonStartResult> CreateGuiStartupBlockedFailureAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonGuiStartupBlockerObservation blockerObservation,
        string launchAttemptId,
        DateTimeOffset launchStartedAtUtc,
        DaemonStartupBlockedProcessPolicy onStartupBlocked)
    {
        ArgumentNullException.ThrowIfNull(blockerObservation);

        var classification = blockerObservation.Classification;

        var primaryError = ExecutionError.InternalError(
            classification.Message,
            ResolveGuiStartupBlockedErrorCode(blockerObservation));
        var updatedAtUtc = timeProvider.GetUtcNow();
        var diagnosis = new DaemonDiagnosis(
            Reason: classification.Reason,
            Message: classification.Message,
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: updatedAtUtc,
            ProcessId: blockerObservation.ProcessId,
            EditorInstancePath: UnityEditorInstanceMarkerPath.Resolve(unityProject.UnityProjectRoot),
            SessionIssuedAtUtc: updatedAtUtc,
            ProcessStartedAtUtc: blockerObservation.ProcessStartedAtUtc,
            UnityLogPath: blockerObservation.UnityLogPath,
            StartupPhase: classification.StartupPhase,
            ActionRequired: classification.ActionRequired,
            PrimaryDiagnostic: classification.PrimaryDiagnostic);
        var processPolicyResult = await ApplyGuiStartupBlockedProcessPolicyAsync(
                unityProject,
                blockerObservation,
                onStartupBlocked,
                CancellationToken.None)
            .ConfigureAwait(false);
        var diagnosisWriteResult = await WriteDiagnosisAsync(unityProject, diagnosis).ConfigureAwait(false);
        var startup = CreateGuiStartupBlockedObservation(
            blockerObservation,
            launchAttemptId,
            processPolicyResult.ProcessAction,
            launchStartedAtUtc,
            updatedAtUtc,
            CreateLaunchAttemptArtifactPath(unityProject, launchAttemptId));
        var launchAttemptWriteResult = await WriteLaunchAttemptAsync(
                unityProject,
                launchAttemptId,
                launchStartedAtUtc,
                updatedAtUtc,
                startup.StartupStatus,
                startup.StartupBlockingReason!,
                startup.RetryDisposition,
                startup.ProcessAction,
                DaemonEditorMode.Gui,
                blockerObservation.ProcessId,
                blockerObservation.ProcessStartedAtUtc,
                blockerObservation.UnityLogPath,
                diagnosis)
            .ConfigureAwait(false);
        var cleanupResult = processPolicyResult.CleanupResult;
        if (!launchAttemptWriteResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                "GUI startup is blocked and launch-attempt artifact persistence failed. " +
                $"StartupError={primaryError.Message} " +
                $"ArtifactError={launchAttemptWriteResult.Error!.Message}"), diagnosis, startup);
        }

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

    private async ValueTask<StartupBlockedProcessPolicyResult> ApplyGuiStartupBlockedProcessPolicyAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonGuiStartupBlockerObservation blockerObservation,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (blockerObservation.Classification.StartupBlockingReason == DaemonStartupBlockingReason.ProcessExit)
        {
            var cleanupResult = await RunLaunchCompensationAsync(
                    unityProject,
                    expectedSession: null,
                    target: null,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return new StartupBlockedProcessPolicyResult(
                cleanupResult,
                DaemonStartupProcessAction.None);
        }

        var policyResolution = DaemonStartupBlockedProcessPolicyResolver.Resolve(
            onStartupBlocked,
            DaemonEditorMode.Gui,
            DaemonSessionOwnerKind.Cli,
            canShutdownProcess: true,
            blockerObservation.ProcessId);
        if (!policyResolution.ShouldTerminateProcess)
        {
            // NOTE: GUI blockers are kept by default so users can resolve Safe Mode, modal, or project errors in Unity.
            return new StartupBlockedProcessPolicyResult(
                CleanupResult: null,
                ProcessAction: policyResolution.ProcessActionWhenNotTerminated);
        }

        var terminationResult = await RunLaunchCompensationAsync(
                unityProject,
                expectedSession: null,
                target: CreateTerminationTarget(blockerObservation.ProcessId, blockerObservation.ProcessStartedAtUtc),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new StartupBlockedProcessPolicyResult(
            terminationResult,
            terminationResult.IsSuccess
                ? DaemonStartupProcessAction.Terminated
                : DaemonStartupProcessAction.Unknown);
    }

    private static UcliCode ResolveGuiStartupBlockedErrorCode (DaemonGuiStartupBlockerObservation blockerObservation)
    {
        ArgumentNullException.ThrowIfNull(blockerObservation);
        return blockerObservation.Classification.StartupBlockingReason == DaemonStartupBlockingReason.ProcessExit
            ? DaemonErrorCodes.DaemonStartProcessExited
            : DaemonErrorCodes.DaemonStartupBlocked;
    }

    private static DaemonStartupObservation CreateGuiStartupBlockedObservation (
        DaemonGuiStartupBlockerObservation blockerObservation,
        string launchAttemptId,
        DaemonStartupProcessAction processAction,
        DateTimeOffset launchStartedAtUtc,
        DateTimeOffset updatedAtUtc,
        string artifactPath)
    {
        ArgumentNullException.ThrowIfNull(blockerObservation);
        ArgumentException.ThrowIfNullOrWhiteSpace(launchAttemptId);
        return CreateStartupFailureObservation(
            DaemonStartupStatus.Blocked,
            blockerObservation.Classification.StartupBlockingReason,
            launchAttemptId,
            processAction,
            blockerObservation.Classification.RetryDisposition,
            DaemonEditorMode.Gui,
            DaemonSessionOwnerKind.Cli,
            true,
            blockerObservation.ProcessId,
            blockerObservation.ProcessStartedAtUtc,
            launchStartedAtUtc,
            updatedAtUtc,
            artifactPath);
    }

    private string CreateUniqueLaunchAttemptId (
        ResolvedUnityProjectContext unityProject,
        DateTimeOffset launchStartedAtUtc)
    {
        const int maxAttempts = 16;
        for (var i = 0; i < maxAttempts; i++)
        {
            var launchAttemptId = launchAttemptIdGenerator.Create(launchStartedAtUtc);
            var launchAttemptDirectory = UcliStoragePathResolver.ResolveLaunchAttemptDirectory(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                launchAttemptId);
            if (!Directory.Exists(launchAttemptDirectory))
            {
                return launchAttemptId;
            }
        }

        return launchAttemptIdGenerator.Create(launchStartedAtUtc);
    }

    private static async ValueTask EmitLaunchingAsync (
        IDaemonStartProgressObserver? progressObserver,
        string launchAttemptId,
        DaemonEditorMode editorMode,
        DaemonSessionOwnerKind ownerKind,
        bool? canShutdownProcess,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        CancellationToken cancellationToken)
    {
        if (progressObserver is null)
        {
            return;
        }

        await progressObserver.EmitLaunchingAsync(
                CreateStartupProgressObservation(
                    launchAttemptId,
                    editorMode,
                    ownerKind,
                    canShutdownProcess,
                    processId,
                    processStartedAtUtc,
                    startupStatus: null,
                    startupBlockingReason: null,
                    startupPhase: null,
                    retryDisposition: null,
                    message: null,
                    errorCode: null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask EmitWaitingForEndpointAsync (
        IDaemonStartProgressObserver? progressObserver,
        string launchAttemptId,
        DaemonEditorMode editorMode,
        DaemonSessionOwnerKind ownerKind,
        bool? canShutdownProcess,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        CancellationToken cancellationToken)
    {
        if (progressObserver is null)
        {
            return;
        }

        await progressObserver.EmitWaitingForEndpointAsync(
                CreateStartupProgressObservation(
                    launchAttemptId,
                    editorMode,
                    ownerKind,
                    canShutdownProcess,
                    processId,
                    processStartedAtUtc,
                    startupStatus: null,
                    startupBlockingReason: null,
                    startupPhase: null,
                    retryDisposition: null,
                    message: null,
                    errorCode: null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask EmitSessionRegisteredAsync (
        IDaemonStartProgressObserver? progressObserver,
        DaemonSession session,
        string launchAttemptId,
        CancellationToken cancellationToken)
    {
        if (progressObserver is null)
        {
            return;
        }

        await progressObserver.EmitSessionRegisteredAsync(session, launchAttemptId, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask EmitEndpointReadyAsync (
        IDaemonStartProgressObserver? progressObserver,
        DaemonSession session,
        string launchAttemptId,
        IpcUnityEditorObservation? lifecycleObservation,
        bool emitSessionRegistered,
        CancellationToken cancellationToken)
    {
        if (progressObserver is null)
        {
            return;
        }

        if (emitSessionRegistered)
        {
            await progressObserver.EmitSessionRegisteredAsync(session, launchAttemptId, cancellationToken).ConfigureAwait(false);
        }

        await progressObserver.EmitEndpointRegisteredAsync(session, launchAttemptId, cancellationToken).ConfigureAwait(false);
        if (lifecycleObservation is not null)
        {
            await progressObserver.EmitLifecycleObservedAsync(lifecycleObservation, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask EmitBatchmodeBlockerDetectedAsync (
        IDaemonStartProgressObserver? progressObserver,
        DaemonStartupFailureClassification classification,
        ExecutionError error,
        string launchAttemptId,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        CancellationToken cancellationToken)
    {
        if (progressObserver is null)
        {
            return;
        }

        await progressObserver.EmitBlockerDetectedAsync(
                CreateStartupProgressObservation(
                    launchAttemptId,
                    DaemonEditorMode.Batchmode,
                    DaemonSessionOwnerKind.Cli,
                    canShutdownProcess: true,
                    processId,
                    processStartedAtUtc,
                    DaemonStartupStatus.Blocked,
                    classification.StartupBlockingReason,
                    classification.StartupPhase,
                    classification.RetryDisposition,
                    classification.Message,
                    ExecutionErrorCodeMapper.ToCode(error).Value),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask EmitGuiBlockerDetectedAsync (
        IDaemonStartProgressObserver? progressObserver,
        DaemonGuiStartupBlockerObservation blockerObservation,
        string launchAttemptId,
        CancellationToken cancellationToken)
    {
        if (progressObserver is null)
        {
            return;
        }

        await progressObserver.EmitBlockerDetectedAsync(
                CreateStartupProgressObservation(
                    launchAttemptId,
                    DaemonEditorMode.Gui,
                    DaemonSessionOwnerKind.Cli,
                    canShutdownProcess: true,
                    blockerObservation.ProcessId,
                    blockerObservation.ProcessStartedAtUtc,
                    DaemonStartupStatus.Blocked,
                    blockerObservation.Classification.StartupBlockingReason,
                    blockerObservation.Classification.StartupPhase,
                    blockerObservation.Classification.RetryDisposition,
                    blockerObservation.Classification.Message,
                    ResolveGuiStartupBlockedErrorCode(blockerObservation).Value),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static DaemonStartStartupProgressObservation CreateStartupProgressObservation (
        string launchAttemptId,
        DaemonEditorMode editorMode,
        DaemonSessionOwnerKind ownerKind,
        bool? canShutdownProcess,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        DaemonStartupStatus? startupStatus,
        DaemonStartupBlockingReason? startupBlockingReason,
        DaemonDiagnosisStartupPhase? startupPhase,
        DaemonStartupRetryDisposition? retryDisposition,
        string? message,
        string? errorCode)
    {
        return new DaemonStartStartupProgressObservation(
            LaunchAttemptId: launchAttemptId,
            EditorMode: editorMode,
            OwnerKind: ownerKind,
            CanShutdownProcess: canShutdownProcess,
            ProcessId: processId,
            ProcessStartedAtUtc: processStartedAtUtc,
            StartupStatus: startupStatus,
            StartupBlockingReason: startupBlockingReason,
            StartupPhase: startupPhase,
            RetryDisposition: retryDisposition,
            Message: message,
            ErrorCode: errorCode);
    }

    private sealed record StartupBlockedProcessPolicyResult (
        DaemonSessionStoreOperationResult? CleanupResult,
        DaemonStartupProcessAction ProcessAction);

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

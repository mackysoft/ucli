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
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
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
        IDaemonLaunchAttemptIdGenerator launchAttemptIdGenerator,
        IDaemonLaunchAttemptStore launchAttemptStore,
        TimeProvider? timeProvider = null)
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
        this.timeProvider = timeProvider ?? TimeProvider.System;
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
                    editorMode: ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                    initializeSessionResult.Error!,
                    startupStatus: ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed),
                    startupBlockingReason: ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Unknown),
                    retryDisposition: ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown),
                    processAction: ContractLiteralCodec.ToValue(DaemonStartupProcessAction.None))
                .ConfigureAwait(false);
        }
        var session = initializeSessionResult.Session!;
        await EmitSessionRegisteredAsync(progressObserver, session, launchAttemptId, cancellationToken).ConfigureAwait(false);
        var launchedProcessId = default(int?);
        var launchedProcessStartedAtUtc = default(DateTimeOffset?);
        var expectedIssuedAtUtc = session.IssuedAtUtc;

        try
        {
            var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint);
            await EmitLaunchingAsync(
                    progressObserver,
                    launchAttemptId,
                    ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                    ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
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
                        expectedIssuedAtUtc,
                        launchAttemptId,
                        launchStartedAtUtc,
                        ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                        unityLogPath,
                        launchResult.Error!,
                        ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed),
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
                        launchAttemptId,
                        launchStartedAtUtc,
                        ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                        unityLogPath,
                        updateProcessIdResult.Error!,
                        ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed),
                        "Daemon session update failed",
                        "SessionError")
                    .ConfigureAwait(false);
            }

            session = updateProcessIdResult.Session!;
            expectedIssuedAtUtc = session.IssuedAtUtc;
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
                        expectedIssuedAtUtc,
                        launchAttemptId,
                        launchStartedAtUtc,
                        ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                        unityLogPath,
                        ExecutionError.Timeout("Timed out before daemon startup readiness probe could begin."),
                        ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout),
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
                        probeResult.LifecycleSnapshot,
                        emitSessionRegistered: false,
                        cancellationToken)
                    .ConfigureAwait(false);
                return DaemonStartResult.Started(session, probeResult.LifecycleSnapshot);
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
                        expectedIssuedAtUtc,
                        launchAttemptId,
                        launchStartedAtUtc,
                        unityLogPath)
                    .ConfigureAwait(false);
            }

            return await CreateFailureWithCompensationAsync(
                    unityProject,
                    launchedProcessId,
                    launchedProcessStartedAtUtc,
                    expectedIssuedAtUtc,
                    launchAttemptId,
                    launchStartedAtUtc,
                    ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                    unityLogPath,
                    probeResult.Error!,
                    probeResult.Error!.Kind == ExecutionErrorKind.Timeout
                        ? ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout)
                        : ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed),
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
                ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
                ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
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
                    ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
                    launchResult.Error!,
                    startupStatus: ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed),
                    startupBlockingReason: ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Unknown),
                    retryDisposition: ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown),
                    processAction: ContractLiteralCodec.ToValue(DaemonStartupProcessAction.None))
                .ConfigureAwait(false);
        }

        var processId = launchResult.ProcessId!.Value;
        var processStartedAtUtc = launchResult.ProcessStartedAtUtc!.Value;
        await EmitWaitingForEndpointAsync(
                progressObserver,
                launchAttemptId,
                ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
                ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
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
            await EmitEndpointReadyAsync(
                    progressObserver,
                    waitResult.Session!,
                    launchAttemptId,
                    waitResult.LifecycleSnapshot,
                    emitSessionRegistered: true,
                    cancellationToken)
                .ConfigureAwait(false);
            return DaemonStartResult.Started(waitResult.Session!, waitResult.LifecycleSnapshot);
        }

        if (waitResult.IsBlocked)
        {
            await EmitGuiBlockerDetectedAsync(progressObserver, waitResult.Blocker!, launchAttemptId, cancellationToken).ConfigureAwait(false);
            return await CreateGuiStartupBlockedFailureAsync(
                    unityProject,
                    waitResult.Blocker!,
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

    private async ValueTask<DaemonStartResult> CreateFailureWithCompensationAsync (
        ResolvedUnityProjectContext unityProject,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        DateTimeOffset expectedIssuedAtUtc,
        string launchAttemptId,
        DateTimeOffset launchStartedAtUtc,
        string editorMode,
        string? unityLogPath,
        ExecutionError primaryError,
        string startupStatus,
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
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath);
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
        var processAction = ResolveCompensatedProcessAction(processId, compensationResult);
        var startup = CreateStartupFailureObservation(
            startupStatus,
            ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Unknown),
            launchAttemptId,
            processAction,
            ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown),
            editorMode,
            ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
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
                ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Unknown),
                ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown),
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
        string? editorMode,
        ExecutionError primaryError,
        string startupStatus,
        string startupBlockingReason,
        string retryDisposition,
        string processAction)
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
            ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
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
        string startupStatus,
        string startupBlockingReason,
        string retryDisposition,
        string processAction,
        string? editorMode,
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
        var writeResult = await launchAttemptStore.WriteFailureAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                launchAttempt,
                CancellationToken.None)
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
                CancellationToken.None)
            .ConfigureAwait(false);
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
        string startupStatus,
        string startupBlockingReason,
        string launchAttemptId,
        string processAction,
        string retryDisposition,
        string? editorMode,
        string? ownerKind,
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
        DateTimeOffset sessionIssuedAtUtc,
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
            SessionIssuedAtUtc: sessionIssuedAtUtc,
            ProcessStartedAtUtc: processStartedAtUtc,
            UnityLogPath: unityLogPath,
            StartupPhase: classification.StartupPhase,
            ActionRequired: classification.ActionRequired,
            PrimaryDiagnostic: classification.PrimaryDiagnostic);
        var diagnosisWriteResult = await daemonDiagnosisStore.WriteAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                diagnosis,
                CancellationToken.None)
            .ConfigureAwait(false);
        var policyResolution = DaemonStartupBlockedProcessPolicyResolver.Resolve(
            onStartupBlocked,
            ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
            ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
            canShutdownProcess: true,
            processId);
        var initialProcessAction = policyResolution.ShouldTerminateProcess
            ? ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown)
            : policyResolution.ProcessActionWhenNotTerminated;
        var launchAttemptWriteResult = await WriteLaunchAttemptAsync(
                unityProject,
                launchAttemptId,
                launchStartedAtUtc,
                updatedAtUtc,
                ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
                classification.StartupBlockingReason,
                classification.RetryDisposition,
                initialProcessAction,
                ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                processId,
                processStartedAtUtc,
                unityLogPath,
                diagnosis,
                pruneAfterWrite: !policyResolution.ShouldTerminateProcess)
            .ConfigureAwait(false);
        // NOTE: Classified batchmode blockers are hard blockers. Persistence failures are reported as secondary
        // errors, but they do not disable termination after both persistence operations have been attempted.
        var policyResult = await ApplyBatchmodeStartupBlockedProcessPolicyAsync(
                unityProject,
                policyResolution,
                processId,
                processStartedAtUtc,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (policyResolution.ShouldTerminateProcess)
        {
            var finalLaunchAttemptWriteResult = await WriteLaunchAttemptAsync(
                    unityProject,
                    launchAttemptId,
                    launchStartedAtUtc,
                    updatedAtUtc,
                    ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
                    classification.StartupBlockingReason,
                    classification.RetryDisposition,
                    policyResult.ProcessAction,
                    ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                    processId,
                    processStartedAtUtc,
                    unityLogPath,
                    diagnosis)
                .ConfigureAwait(false);
            if (launchAttemptWriteResult.IsSuccess)
            {
                launchAttemptWriteResult = finalLaunchAttemptWriteResult;
            }
        }

        var startup = CreateStartupFailureObservation(
            ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
            classification.StartupBlockingReason,
            launchAttemptId,
            policyResult.ProcessAction,
            classification.RetryDisposition,
            ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
            ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
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

        var cleanupResult = await daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                unityProject,
                CreateTerminationTarget(processId, processStartedAtUtc),
                DaemonTimeouts.LaunchCompensationTimeout,
                cancellationToken)
            .ConfigureAwait(false);

        return new StartupBlockedProcessPolicyResult(
            cleanupResult,
            ResolveCompensatedProcessAction(processId, cleanupResult));
    }

    private static string ResolveCompensatedProcessAction (
        int? processId,
        DaemonSessionStoreOperationResult compensationResult)
    {
        ArgumentNullException.ThrowIfNull(compensationResult);
        if (processId is null)
        {
            return ContractLiteralCodec.ToValue(DaemonStartupProcessAction.None);
        }

        return compensationResult.IsSuccess
            ? ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated)
            : ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown);
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
        string launchAttemptId,
        DateTimeOffset launchStartedAtUtc,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        ExecutionError waitError)
    {
        var startResult = await CreateGuiEndpointNotRegisteredFailureAsync(
                unityProject,
                processId,
                processStartedAtUtc,
                unityLogPath,
                waitError)
            .ConfigureAwait(false);
        var policyResolution = DaemonStartupBlockedProcessPolicyResolver.Resolve(
            onStartupBlocked,
            ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
            ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
            canShutdownProcess: true,
            processId);
        var compensationResult = policyResolution.ShouldTerminateProcess
            ? await daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                    unityProject,
                    CreateTerminationTarget(processId, processStartedAtUtc),
                    DaemonTimeouts.LaunchCompensationTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false)
            : null;
        var processAction = compensationResult is null
            ? policyResolution.ProcessActionWhenNotTerminated
            : ResolveCompensatedProcessAction(processId, compensationResult);
        var updatedAtUtc = timeProvider.GetUtcNow();
        var startup = CreateStartupFailureObservation(
            ContractLiteralCodec.ToValue(DaemonStartupStatus.Timeout),
            ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.EndpointNotRegistered),
            launchAttemptId,
            processAction,
            ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.WaitThenRetry),
            ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
            ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
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
                ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
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
        var compensationResult = await daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                unityProject,
                CreateTerminationTarget(processId, processStartedAtUtc),
                DaemonTimeouts.LaunchCompensationTimeout,
                CancellationToken.None)
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
                ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
                startupError,
                ContractLiteralCodec.ToValue(DaemonStartupStatus.Failed),
                ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Unknown),
                ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.Unknown),
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
        DaemonGuiStartupBlocker blocker,
        string launchAttemptId,
        DateTimeOffset launchStartedAtUtc,
        DaemonStartupBlockedProcessPolicy onStartupBlocked)
    {
        ArgumentNullException.ThrowIfNull(blocker);

        var primaryError = ExecutionError.InternalError(
            blocker.Message,
            ResolveGuiStartupBlockedErrorCode(blocker));
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
                ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
                blocker.ProcessId,
                blocker.ProcessStartedAtUtc,
                blocker.UnityLogPath,
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
        DaemonGuiStartupBlocker blocker,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ContractLiteralCodec.Matches(blocker.StartupBlockingReason, DaemonStartupBlockingReason.ProcessExit))
        {
            var cleanupResult = await daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                    unityProject,
                    target: null,
                    DaemonTimeouts.LaunchCompensationTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            return new StartupBlockedProcessPolicyResult(
                cleanupResult,
                ContractLiteralCodec.ToValue(DaemonStartupProcessAction.None));
        }

        var policyResolution = DaemonStartupBlockedProcessPolicyResolver.Resolve(
            onStartupBlocked,
            ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
            ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
            canShutdownProcess: true,
            blocker.ProcessId);
        if (!policyResolution.ShouldTerminateProcess)
        {
            // NOTE: GUI blockers are kept by default so users can resolve Safe Mode, modal, or project errors in Unity.
            return new StartupBlockedProcessPolicyResult(
                CleanupResult: null,
                ProcessAction: policyResolution.ProcessActionWhenNotTerminated);
        }

        var terminationResult = await daemonLaunchCompensationService.CleanupFailedLaunchAsync(
                unityProject,
                CreateTerminationTarget(blocker.ProcessId, blocker.ProcessStartedAtUtc),
                DaemonTimeouts.LaunchCompensationTimeout,
                cancellationToken)
            .ConfigureAwait(false);

        return new StartupBlockedProcessPolicyResult(
            terminationResult,
            terminationResult.IsSuccess
                ? ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Terminated)
                : ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown));
    }

    private static UcliCode ResolveGuiStartupBlockedErrorCode (DaemonGuiStartupBlocker blocker)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        return ContractLiteralCodec.Matches(blocker.StartupBlockingReason, DaemonStartupBlockingReason.ProcessExit)
            ? DaemonErrorCodes.DaemonStartProcessExited
            : DaemonErrorCodes.DaemonStartupBlocked;
    }

    private static DaemonStartupObservation CreateGuiStartupBlockedObservation (
        DaemonGuiStartupBlocker blocker,
        string launchAttemptId,
        string processAction,
        DateTimeOffset launchStartedAtUtc,
        DateTimeOffset updatedAtUtc,
        string artifactPath)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        ArgumentException.ThrowIfNullOrWhiteSpace(launchAttemptId);
        ArgumentException.ThrowIfNullOrWhiteSpace(processAction);

        return CreateStartupFailureObservation(
            ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
            blocker.StartupBlockingReason,
            launchAttemptId,
            processAction,
            blocker.RetryDisposition,
            ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
            ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
            true,
            blocker.ProcessId,
            blocker.ProcessStartedAtUtc,
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
        string editorMode,
        string ownerKind,
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
        string editorMode,
        string ownerKind,
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
        DaemonStartLifecycleSnapshot? lifecycleSnapshot,
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
        if (lifecycleSnapshot is not null)
        {
            await progressObserver.EmitLifecycleObservedAsync(lifecycleSnapshot, cancellationToken).ConfigureAwait(false);
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
                    ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode),
                    ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
                    canShutdownProcess: true,
                    processId,
                    processStartedAtUtc,
                    ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
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
        DaemonGuiStartupBlocker blocker,
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
                    ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
                    ContractLiteralCodec.ToValue(DaemonSessionOwnerKind.Cli),
                    canShutdownProcess: true,
                    blocker.ProcessId,
                    blocker.ProcessStartedAtUtc,
                    ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
                    blocker.StartupBlockingReason,
                    blocker.StartupPhase,
                    blocker.RetryDisposition,
                    blocker.Message,
                    ResolveGuiStartupBlockedErrorCode(blocker).Value),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static DaemonStartStartupProgressObservation CreateStartupProgressObservation (
        string launchAttemptId,
        string editorMode,
        string ownerKind,
        bool? canShutdownProcess,
        int? processId,
        DateTimeOffset? processStartedAtUtc,
        string? startupStatus,
        string? startupBlockingReason,
        string? startupPhase,
        string? retryDisposition,
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

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start;

/// <summary> Implements daemon launch workflow with failure-compensation handling. </summary>
internal sealed class DaemonLaunchService : IDaemonLaunchService
{
    private readonly IDaemonLaunchSessionService daemonLaunchSessionService;

    private readonly IUnityDaemonProcessLauncher unityDaemonProcessLauncher;

    private readonly IUnityGuiEditorProcessLauncher unityGuiEditorProcessLauncher;

    private readonly IDaemonStartupReadinessProbe startupReadinessProbe;

    private readonly IDaemonGuiSessionRegistrationAwaiter guiSessionRegistrationAwaiter;

    private readonly IDaemonLaunchCompensationService daemonLaunchCompensationService;

    private readonly IDaemonDiagnosisStore daemonDiagnosisStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonLaunchService" /> class. </summary>
    /// <param name="daemonLaunchSessionService"> The daemon launch-session service dependency. </param>
    /// <param name="unityDaemonProcessLauncher"> The Unity daemon process-launcher dependency. </param>
    /// <param name="unityGuiEditorProcessLauncher"> The Unity GUI Editor process-launcher dependency. </param>
    /// <param name="startupReadinessProbe"> The daemon startup-readiness probe dependency. </param>
    /// <param name="guiSessionRegistrationAwaiter"> The GUI session-registration awaiter dependency. </param>
    /// <param name="daemonLaunchCompensationService"> The daemon launch-compensation service dependency. </param>
    /// <param name="daemonDiagnosisStore"> The daemon diagnosis store dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting and timestamps. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonLaunchService (
        IDaemonLaunchSessionService daemonLaunchSessionService,
        IUnityDaemonProcessLauncher unityDaemonProcessLauncher,
        IUnityGuiEditorProcessLauncher unityGuiEditorProcessLauncher,
        IDaemonStartupReadinessProbe startupReadinessProbe,
        IDaemonGuiSessionRegistrationAwaiter guiSessionRegistrationAwaiter,
        IDaemonLaunchCompensationService daemonLaunchCompensationService,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        TimeProvider? timeProvider = null)
    {
        this.daemonLaunchSessionService = daemonLaunchSessionService ?? throw new ArgumentNullException(nameof(daemonLaunchSessionService));
        this.unityDaemonProcessLauncher = unityDaemonProcessLauncher ?? throw new ArgumentNullException(nameof(unityDaemonProcessLauncher));
        this.unityGuiEditorProcessLauncher = unityGuiEditorProcessLauncher ?? throw new ArgumentNullException(nameof(unityGuiEditorProcessLauncher));
        this.startupReadinessProbe = startupReadinessProbe ?? throw new ArgumentNullException(nameof(startupReadinessProbe));
        this.guiSessionRegistrationAwaiter = guiSessionRegistrationAwaiter ?? throw new ArgumentNullException(nameof(guiSessionRegistrationAwaiter));
        this.daemonLaunchCompensationService = daemonLaunchCompensationService ?? throw new ArgumentNullException(nameof(daemonLaunchCompensationService));
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Launches daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon startup timeout. </param>
    /// <param name="editorMode"> The requested daemon Editor mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartResult> Launch (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode editorMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);

        return editorMode switch
        {
            DaemonEditorMode.Batchmode => await LaunchBatchmode(
                    unityProject,
                    editorMode,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false),
            DaemonEditorMode.Gui => await LaunchGui(
                    unityProject,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => DaemonStartResult.Failure(ExecutionError.InvalidArgument(
                $"daemon start editorMode is invalid. Actual: {editorMode}.")),
        };
    }

    private async ValueTask<DaemonStartResult> LaunchBatchmode (
        ResolvedUnityProjectContext unityProject,
        DaemonEditorMode editorMode,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var initializeSessionResult = await daemonLaunchSessionService.Initialize(
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
        var expectedIssuedAtUtc = session.IssuedAtUtc;

        try
        {
            var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint);
            var launchResult = await unityDaemonProcessLauncher.Launch(
                    unityProject,
                    session,
                    unityLogPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!launchResult.IsSuccess)
            {
                return await CreateFailureWithCompensation(
                        unityProject,
                        launchResult.ProcessId,
                        expectedIssuedAtUtc,
                        launchResult.Error!,
                        "Daemon launch failed",
                        "LaunchError")
                    .ConfigureAwait(false);
            }

            if (launchResult.ProcessId is not int processId)
            {
                return DaemonStartResult.Failure(ExecutionError.InternalError(
                    "Unity daemon launch succeeded without a process identifier."));
            }

            launchedProcessId = processId;
            var updateProcessIdResult = await daemonLaunchSessionService.UpdateProcessId(
                    unityProject,
                    session,
                    launchedProcessId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!updateProcessIdResult.IsSuccess)
            {
                return await CreateFailureWithCompensation(
                        unityProject,
                        launchedProcessId,
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
                return await CreateFailureWithCompensation(
                        unityProject,
                        launchedProcessId,
                        expectedIssuedAtUtc,
                        ExecutionError.Timeout("Timed out before daemon startup readiness probe could begin."),
                        "Daemon startup readiness probe failed",
                        "ProbeError")
                    .ConfigureAwait(false);
            }

            var probeResult = await startupReadinessProbe.WaitUntilReady(
                    unityProject,
                    probeTimeout,
                    launchedProcessId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (probeResult.IsReady)
            {
                return DaemonStartResult.Started(session);
            }

            return await CreateFailureWithCompensation(
                    unityProject,
                    launchedProcessId,
                    expectedIssuedAtUtc,
                    probeResult.Error!,
                    "Daemon startup readiness probe failed",
                    "ProbeError")
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await daemonLaunchCompensationService.CleanupFailedLaunch(
                    unityProject,
                    launchedProcessId,
                    expectedIssuedAtUtc,
                    DaemonTimeouts.LaunchCompensationTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<DaemonStartResult> LaunchGui (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var launchedProcessId = default(int?);
        try
        {
            var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint);
            var launchResult = await unityGuiEditorProcessLauncher.Launch(
                    unityProject,
                    unityLogPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!launchResult.IsSuccess)
            {
                return DaemonStartResult.Failure(launchResult.Error!);
            }

            if (launchResult.ProcessId is not int processId)
            {
                return DaemonStartResult.Failure(ExecutionError.InternalError(
                    "Unity GUI Editor launch succeeded without a process identifier."));
            }

            launchedProcessId = processId;
            if (!deadline.TryGetRemainingTimeout(out var waitTimeout))
            {
                return await CreateGuiEndpointNotRegisteredFailure(
                        unityProject,
                        launchedProcessId,
                        ExecutionError.Timeout(
                            "Timed out before GUI daemon session registration wait could begin.",
                            ExecutionErrorCodes.IpcTimeout))
                    .ConfigureAwait(false);
            }

            var waitResult = await guiSessionRegistrationAwaiter.WaitForSession(
                    unityProject,
                    processId,
                    waitTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (waitResult.IsSuccess)
            {
                return DaemonStartResult.Started(waitResult.Session!);
            }

            if (waitResult.Error!.Kind == ExecutionErrorKind.Timeout)
            {
                return await CreateGuiEndpointNotRegisteredFailure(
                        unityProject,
                        launchedProcessId,
                        waitResult.Error)
                    .ConfigureAwait(false);
            }

            return DaemonStartResult.Failure(waitResult.Error);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }

    private async ValueTask<DaemonStartResult> CreateFailureWithCompensation (
        ResolvedUnityProjectContext unityProject,
        int? processId,
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
            SessionIssuedAtUtc: expectedIssuedAtUtc);
        var diagnosisWriteResult = await daemonDiagnosisStore.Write(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                diagnosis,
                CancellationToken.None)
            .ConfigureAwait(false);
        var compensationResult = await daemonLaunchCompensationService.CleanupFailedLaunch(
                unityProject,
                processId,
                expectedIssuedAtUtc,
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

    private async ValueTask<DaemonStartResult> CreateGuiEndpointNotRegisteredFailure (
        ResolvedUnityProjectContext unityProject,
        int? processId,
        ExecutionError waitError)
    {
        var editorInstancePath = ResolveEditorInstancePath(unityProject.UnityProjectRoot);
        var timeoutError = DaemonGuiEndpointNotRegisteredFailureFactory.CreateTimeoutError(
            "GUI Editor",
            editorInstancePath,
            processId,
            waitError);
        var updatedAtUtc = timeProvider.GetUtcNow();
        var diagnosis = DaemonGuiEndpointNotRegisteredFailureFactory.CreateDiagnosis(
            timeoutError.Message,
            processId,
            editorInstancePath,
            updatedAtUtc);
        var diagnosisWriteResult = await daemonDiagnosisStore.Write(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                diagnosis,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (!diagnosisWriteResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                timeoutError,
                "GUI Editor endpoint registration timed out and diagnosis persistence failed. " +
                $"StartError={timeoutError.Message} DiagnosisError={diagnosisWriteResult.Error!.Message}"), diagnosis);
        }

        return DaemonStartResult.Failure(timeoutError, diagnosis);
    }

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

    private static string ResolveEditorInstancePath (string unityProjectRoot)
    {
        return Path.Combine(unityProjectRoot, "Library", "EditorInstance.json");
    }
}

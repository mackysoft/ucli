using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.ExistingSession;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiAttach;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Launch;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Implements daemon start workflow orchestration for one project fingerprint. </summary>
internal sealed class DaemonStartOperation : IDaemonStartOperation
{
    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    private readonly IDaemonDiagnosisStore daemonDiagnosisStore;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonSessionCleanupService daemonSessionCleanupService;

    private readonly IDaemonExistingSessionGateService daemonExistingSessionGateService;

    private readonly IDaemonGuiEditorAttachService daemonGuiEditorAttachService;

    private readonly IDaemonLaunchService daemonLaunchService;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="daemonSessionCleanupService"> The daemon session-cleanup service dependency. </param>
    /// <param name="daemonExistingSessionGateService"> The daemon existing-session gate service dependency. </param>
    /// <param name="daemonGuiEditorAttachService"> The daemon GUI Editor attach service dependency. </param>
    /// <param name="daemonLaunchService"> The daemon launch service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartOperation (
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        IDaemonSessionStore daemonSessionStore,
        IDaemonSessionCleanupService daemonSessionCleanupService,
        IDaemonExistingSessionGateService daemonExistingSessionGateService,
        IDaemonGuiEditorAttachService daemonGuiEditorAttachService,
        IDaemonLaunchService daemonLaunchService)
    {
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonSessionCleanupService = daemonSessionCleanupService ?? throw new ArgumentNullException(nameof(daemonSessionCleanupService));
        this.daemonExistingSessionGateService = daemonExistingSessionGateService ?? throw new ArgumentNullException(nameof(daemonExistingSessionGateService));
        this.daemonGuiEditorAttachService = daemonGuiEditorAttachService ?? throw new ArgumentNullException(nameof(daemonGuiEditorAttachService));
        this.daemonLaunchService = daemonLaunchService ?? throw new ArgumentNullException(nameof(daemonLaunchService));
    }

    /// <summary> Starts daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon startup timeout. </param>
    /// <param name="editorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="onStartupBlocked"> The startup-blocked process policy requested by the caller. </param>
    /// <param name="progressObserver"> The optional observer for supervisor-internal start progress. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartResult> StartAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonStartProgressObserver? progressObserver = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout);
        if (!deadline.TryGetRemainingTimeout(out var lockAcquireTimeout))
        {
            return DaemonStartResult.Failure(CreateTimeoutError("Timed out before daemon start workflow began."));
        }

        IAsyncDisposable lockHandle;
        try
        {
            lockHandle = await lifecycleLockProvider.AcquireAsync(
                    new ProjectLifecycleLockRequest(unityProject.UnityProjectRoot),
                    lockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return DaemonStartResult.Failure(CreateTimeoutError(
                $"Timed out while waiting for project lifecycle lock. {exception.Message}"));
        }
        catch (Exception exception)
        {
            return DaemonStartResult.Failure(ExecutionError.InternalError(
                $"Failed to acquire project lifecycle lock. {exception.Message}"));
        }

        await using var acquiredLock = lockHandle;
        ExecutionError? diagnosisCleanupError = null;
        var deleteDiagnosisResult = await daemonDiagnosisStore.DeleteAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!deleteDiagnosisResult.IsSuccess)
        {
            // NOTE: Persisted diagnosis is auxiliary metadata from the previous lifecycle.
            // Start must continue so recovery and relaunch are not blocked by sidecar cleanup failures.
            diagnosisCleanupError = deleteDiagnosisResult.Error
                ?? ExecutionError.InternalError("Daemon diagnosis cleanup failed without a structured error.");
        }

        var readResult = await daemonSessionStore.ReadAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return await HandleInvalidSessionReadAsync(
                    unityProject,
                    readResult,
                    deadline,
                    diagnosisCleanupError,
                    editorMode,
                    onStartupBlocked,
                    progressObserver,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (readResult.Exists)
        {
            if (!deadline.TryGetRemainingTimeout(out var existingSessionGateTimeout))
            {
                return CreateFailure(
                    CreateTimeoutError("Timed out while probing existing daemon session."),
                    diagnosisCleanupError);
            }

            var existingSessionGateResult = await daemonExistingSessionGateService.TryHandleExistingSessionAsync(
                    unityProject,
                    readResult.Session!,
                    existingSessionGateTimeout,
                    editorMode,
                    progressObserver,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existingSessionGateResult is not null)
            {
                return CreateResult(existingSessionGateResult, diagnosisCleanupError);
            }
        }

        return await TryAttachExistingGuiEditorOrLaunchAsync(
                unityProject,
                deadline,
                diagnosisCleanupError,
                editorMode,
                onStartupBlocked,
                progressObserver,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonStartResult> HandleInvalidSessionReadAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        ExecutionDeadline deadline,
        ExecutionError? diagnosisCleanupError,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonStartProgressObserver? progressObserver,
        CancellationToken cancellationToken)
    {
        if (readResult.FailureKind != DaemonSessionReadFailureKind.InvalidSession)
        {
            return CreateFailure(readResult.Error!, diagnosisCleanupError);
        }

        if (!deadline.TryGetRemainingTimeout(out var invalidSessionCleanupTimeout))
        {
            return CreateFailure(
                CreateTimeoutError("Timed out while preparing invalid daemon-session cleanup."),
                diagnosisCleanupError);
        }

        var cleanupResult = await daemonSessionCleanupService.CleanupInvalidSessionArtifactsAsync(
                unityProject,
                readResult,
                invalidSessionCleanupTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!cleanupResult.IsSuccess)
        {
            return CreateFailure(cleanupResult.Error!, diagnosisCleanupError);
        }

        return await TryAttachExistingGuiEditorOrLaunchAsync(
                unityProject,
                deadline,
                diagnosisCleanupError,
                editorMode,
                onStartupBlocked,
                progressObserver,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonStartResult> TryAttachExistingGuiEditorOrLaunchAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        ExecutionError? diagnosisCleanupError,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonStartProgressObserver? progressObserver,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var attachTimeout))
        {
            return CreateFailure(
                CreateTimeoutError("Timed out before existing GUI Editor attach could start."),
                diagnosisCleanupError);
        }

        var attachResult = await daemonGuiEditorAttachService.TryAttachExistingGuiEditorAsync(
                unityProject,
                attachTimeout,
                editorMode,
                onStartupBlocked,
                progressObserver,
                cancellationToken)
            .ConfigureAwait(false);
        if (attachResult is not null)
        {
            return CreateResult(attachResult, diagnosisCleanupError);
        }

        if (!deadline.TryGetRemainingTimeout(out var launchTimeout))
        {
            return CreateFailure(
                CreateTimeoutError("Timed out before daemon launch could start."),
                diagnosisCleanupError);
        }

        if (!DaemonLaunchEditorModePolicy.TryResolve(editorMode, out var launchEditorMode, out var editorModeError))
        {
            return CreateFailure(editorModeError!, diagnosisCleanupError);
        }

        var launchResult = await daemonLaunchService.LaunchAsync(
                unityProject,
                launchTimeout,
                launchEditorMode,
                onStartupBlocked,
                progressObserver,
                cancellationToken)
            .ConfigureAwait(false);
        return CreateResult(launchResult, diagnosisCleanupError);
    }

    private static DaemonStartResult CreateFailure (
        ExecutionError error,
        ExecutionError? diagnosisCleanupError)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (diagnosisCleanupError == null)
        {
            return DaemonStartResult.Failure(error);
        }

        return DaemonStartResult.Failure(CreateAugmentedPrimaryError(error, diagnosisCleanupError));
    }

    private static DaemonStartResult CreateResult (
        DaemonStartResult result,
        ExecutionError? diagnosisCleanupError)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (diagnosisCleanupError == null || result.IsSuccess || result.Error == null)
        {
            return result;
        }

        return DaemonStartResult.Failure(
            CreateAugmentedPrimaryError(result.Error, diagnosisCleanupError),
            result.Diagnosis,
            result.Startup);
    }

    private static ExecutionError CreateAugmentedPrimaryError (
        ExecutionError primaryError,
        ExecutionError diagnosisCleanupError)
    {
        ArgumentNullException.ThrowIfNull(primaryError);
        ArgumentNullException.ThrowIfNull(diagnosisCleanupError);

        var message =
            "Daemon diagnosis cleanup failed before start, but startup continued. " +
            $"StartError={primaryError.Message} " +
            $"DiagnosisCleanupError={diagnosisCleanupError.Message}";

        return primaryError.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => ExecutionError.InvalidArgument(message, primaryError.Code),
            ExecutionErrorKind.Timeout => ExecutionError.Timeout(message, primaryError.Code),
            ExecutionErrorKind.InternalError => ExecutionError.InternalError(message, primaryError.Code),
            _ => throw new ArgumentOutOfRangeException(nameof(primaryError), primaryError.Kind, "Unsupported execution error kind."),
        };
    }

    private static ExecutionError CreateTimeoutError (string message)
    {
        return ExecutionError.Timeout(message);
    }
}

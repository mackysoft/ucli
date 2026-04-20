using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start;

/// <summary> Implements daemon start workflow orchestration for one project fingerprint. </summary>
internal sealed class DaemonStartOperation : IDaemonStartOperation
{
    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    private readonly IDaemonDiagnosisStore daemonDiagnosisStore;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonSessionCleanupService daemonSessionCleanupService;

    private readonly IDaemonExistingSessionGateService daemonExistingSessionGateService;

    private readonly IDaemonLaunchService daemonLaunchService;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="daemonSessionCleanupService"> The daemon session-cleanup service dependency. </param>
    /// <param name="daemonExistingSessionGateService"> The daemon existing-session gate service dependency. </param>
    /// <param name="daemonLaunchService"> The daemon launch service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartOperation (
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        IDaemonSessionStore daemonSessionStore,
        IDaemonSessionCleanupService daemonSessionCleanupService,
        IDaemonExistingSessionGateService daemonExistingSessionGateService,
        IDaemonLaunchService daemonLaunchService)
    {
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonSessionCleanupService = daemonSessionCleanupService ?? throw new ArgumentNullException(nameof(daemonSessionCleanupService));
        this.daemonExistingSessionGateService = daemonExistingSessionGateService ?? throw new ArgumentNullException(nameof(daemonExistingSessionGateService));
        this.daemonLaunchService = daemonLaunchService ?? throw new ArgumentNullException(nameof(daemonLaunchService));
    }

    /// <summary> Starts daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon startup timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartResult> Start (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
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
            lockHandle = await lifecycleLockProvider.Acquire(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    lockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            return DaemonStartResult.Failure(CreateTimeoutError(
                $"Timed out while waiting for project lifecycle lock. {exception.Message}"));
        }

        await using var acquiredLock = lockHandle;
        ExecutionError? diagnosisCleanupError = null;
        var deleteDiagnosisResult = await daemonDiagnosisStore.Delete(
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

        var readResult = await daemonSessionStore.Read(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return await HandleInvalidSessionRead(
                    unityProject,
                    readResult,
                    deadline,
                    diagnosisCleanupError,
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

            var existingSessionGateResult = await daemonExistingSessionGateService.TryHandleExistingSession(
                    unityProject,
                    readResult.Session!,
                    existingSessionGateTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (existingSessionGateResult is not null)
            {
                return CreateResult(existingSessionGateResult, diagnosisCleanupError);
            }
        }

        if (!deadline.TryGetRemainingTimeout(out var launchTimeout))
        {
            return CreateFailure(
                CreateTimeoutError("Timed out before daemon launch could start."),
                diagnosisCleanupError);
        }

        var launchResult = await daemonLaunchService.Launch(unityProject, launchTimeout, cancellationToken).ConfigureAwait(false);
        return CreateResult(launchResult, diagnosisCleanupError);
    }

    private async ValueTask<DaemonStartResult> HandleInvalidSessionRead (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        ExecutionDeadline deadline,
        ExecutionError? diagnosisCleanupError,
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

        var cleanupResult = await daemonSessionCleanupService.CleanupInvalidSessionArtifacts(
                unityProject,
                readResult,
                invalidSessionCleanupTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!cleanupResult.IsSuccess)
        {
            return CreateFailure(cleanupResult.Error!, diagnosisCleanupError);
        }

        if (!deadline.TryGetRemainingTimeout(out var launchTimeout))
        {
            return CreateFailure(
                CreateTimeoutError("Timed out before daemon launch could start."),
                diagnosisCleanupError);
        }

        var launchResult = await daemonLaunchService.Launch(unityProject, launchTimeout, cancellationToken).ConfigureAwait(false);
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

        return DaemonStartResult.Failure(CreateAugmentedPrimaryError(result.Error, diagnosisCleanupError));
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
            ExecutionErrorKind.InvalidArgument => ExecutionError.InvalidArgument(message),
            ExecutionErrorKind.Timeout => ExecutionError.Timeout(message),
            ExecutionErrorKind.InternalError => ExecutionError.InternalError(message),
            _ => throw new ArgumentOutOfRangeException(nameof(primaryError), primaryError.Kind, "Unsupported execution error kind."),
        };
    }

    private static ExecutionError CreateTimeoutError (string message)
    {
        return ExecutionError.Timeout(message);
    }
}
using MackySoft.Ucli.Contracts.Storage;
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

/// <summary> Implements daemon launch workflow with failure-compensation handling. </summary>
internal sealed class DaemonLaunchService : IDaemonLaunchService
{
    private readonly IDaemonLaunchSessionService daemonLaunchSessionService;

    private readonly IUnityDaemonProcessLauncher unityDaemonProcessLauncher;

    private readonly IDaemonStartupReadinessProbe startupReadinessProbe;

    private readonly IDaemonLaunchCompensationService daemonLaunchCompensationService;

    private readonly IDaemonDiagnosisStore daemonDiagnosisStore;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonLaunchService" /> class. </summary>
    /// <param name="daemonLaunchSessionService"> The daemon launch-session service dependency. </param>
    /// <param name="unityDaemonProcessLauncher"> The Unity daemon process-launcher dependency. </param>
    /// <param name="startupReadinessProbe"> The daemon startup-readiness probe dependency. </param>
    /// <param name="daemonLaunchCompensationService"> The daemon launch-compensation service dependency. </param>
    /// <param name="daemonDiagnosisStore"> The daemon diagnosis store dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting and timestamps. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonLaunchService (
        IDaemonLaunchSessionService daemonLaunchSessionService,
        IUnityDaemonProcessLauncher unityDaemonProcessLauncher,
        IDaemonStartupReadinessProbe startupReadinessProbe,
        IDaemonLaunchCompensationService daemonLaunchCompensationService,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        TimeProvider? timeProvider = null)
    {
        this.daemonLaunchSessionService = daemonLaunchSessionService ?? throw new ArgumentNullException(nameof(daemonLaunchSessionService));
        this.unityDaemonProcessLauncher = unityDaemonProcessLauncher ?? throw new ArgumentNullException(nameof(unityDaemonProcessLauncher));
        this.startupReadinessProbe = startupReadinessProbe ?? throw new ArgumentNullException(nameof(startupReadinessProbe));
        this.daemonLaunchCompensationService = daemonLaunchCompensationService ?? throw new ArgumentNullException(nameof(daemonLaunchCompensationService));
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Launches daemon lifecycle for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon startup timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartResult> Launch (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);

        var initializeSessionResult = await daemonLaunchSessionService.Initialize(unityProject, cancellationToken).ConfigureAwait(false);
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

            launchedProcessId = launchResult.ProcessId;
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

    private async ValueTask<DaemonStartResult> CreateFailureWithCompensation (
        ResolvedUnityProjectContext unityProject,
        int? processId,
        DateTimeOffset expectedIssuedAtUtc,
        ExecutionError primaryError,
        string primaryErrorMessagePrefix,
        string primaryErrorLabel)
    {
        var diagnosisWriteResult = await daemonDiagnosisStore.Write(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                new DaemonDiagnosis(
                    Reason: DaemonDiagnosisReasonValues.StartupFailed,
                    Message: primaryError.Message,
                    ReportedBy: DaemonDiagnosisReportedByValues.Cli,
                    IsInferred: false,
                    UpdatedAtUtc: timeProvider.GetUtcNow(),
                    ProcessId: processId,
                    SessionIssuedAtUtc: expectedIssuedAtUtc),
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
                $"CleanupError={compensationResult.Error!.Message}"));
        }

        if (!diagnosisWriteResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                $"{primaryErrorMessagePrefix} and diagnosis persistence failed. " +
                $"{primaryErrorLabel}={primaryError.Message} " +
                $"DiagnosisError={diagnosisWriteResult.Error!.Message}"));
        }

        if (!compensationResult.IsSuccess)
        {
            return DaemonStartResult.Failure(CreateAugmentedPrimaryError(
                primaryError,
                $"{primaryErrorMessagePrefix} and cleanup failed. " +
                $"{primaryErrorLabel}={primaryError.Message} " +
                $"CleanupError={compensationResult.Error!.Message}"));
        }

        return DaemonStartResult.Failure(primaryError);
    }

    private static ExecutionError CreateAugmentedPrimaryError (
        ExecutionError primaryError,
        string message)
    {
        ArgumentNullException.ThrowIfNull(primaryError);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return primaryError.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => ExecutionError.InvalidArgument(message),
            ExecutionErrorKind.Timeout => ExecutionError.Timeout(message),
            ExecutionErrorKind.InternalError => ExecutionError.InternalError(message),
            _ => throw new ArgumentOutOfRangeException(nameof(primaryError), primaryError.Kind, "Unsupported execution error kind."),
        };
    }
}
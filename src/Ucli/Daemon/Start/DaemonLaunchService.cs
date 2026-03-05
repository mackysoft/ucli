using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon.Start;

/// <summary> Implements daemon launch workflow with failure-compensation handling. </summary>
internal sealed class DaemonLaunchService : IDaemonLaunchService
{
    private readonly IDaemonLaunchSessionService daemonLaunchSessionService;

    private readonly IUnityDaemonProcessLauncher unityDaemonProcessLauncher;

    private readonly IDaemonStartupReadinessProbe startupReadinessProbe;

    private readonly IDaemonLaunchCompensationService daemonLaunchCompensationService;

    /// <summary> Initializes a new instance of the <see cref="DaemonLaunchService" /> class. </summary>
    /// <param name="daemonLaunchSessionService"> The daemon launch-session service dependency. </param>
    /// <param name="unityDaemonProcessLauncher"> The Unity daemon process-launcher dependency. </param>
    /// <param name="startupReadinessProbe"> The daemon startup-readiness probe dependency. </param>
    /// <param name="daemonLaunchCompensationService"> The daemon launch-compensation service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonLaunchService (
        IDaemonLaunchSessionService daemonLaunchSessionService,
        IUnityDaemonProcessLauncher unityDaemonProcessLauncher,
        IDaemonStartupReadinessProbe startupReadinessProbe,
        IDaemonLaunchCompensationService daemonLaunchCompensationService)
    {
        this.daemonLaunchSessionService = daemonLaunchSessionService ?? throw new ArgumentNullException(nameof(daemonLaunchSessionService));
        this.unityDaemonProcessLauncher = unityDaemonProcessLauncher ?? throw new ArgumentNullException(nameof(unityDaemonProcessLauncher));
        this.startupReadinessProbe = startupReadinessProbe ?? throw new ArgumentNullException(nameof(startupReadinessProbe));
        this.daemonLaunchCompensationService = daemonLaunchCompensationService ?? throw new ArgumentNullException(nameof(daemonLaunchCompensationService));
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
        var deadline = ExecutionDeadline.Start(timeout);

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
            var daemonLogPath = UcliStoragePathResolver.ResolveDaemonLogPath(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint);
            var launchResult = await unityDaemonProcessLauncher.Launch(
                    unityProject,
                    daemonLogPath,
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

            var probeResult = await startupReadinessProbe.WaitUntilReady(unityProject, probeTimeout, cancellationToken).ConfigureAwait(false);
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
        var compensationResult = await daemonLaunchCompensationService.CleanupFailedLaunch(
                unityProject,
                processId,
                expectedIssuedAtUtc,
                DaemonTimeouts.LaunchCompensationTimeout,
                CancellationToken.None)
            .ConfigureAwait(false);
        if (!compensationResult.IsSuccess)
        {
            return DaemonStartResult.Failure(ExecutionError.InternalError(
                $"{primaryErrorMessagePrefix} and cleanup failed. {primaryErrorLabel}={primaryError.Message} CleanupError={compensationResult.Error!.Message}"));
        }

        return DaemonStartResult.Failure(primaryError);
    }
}
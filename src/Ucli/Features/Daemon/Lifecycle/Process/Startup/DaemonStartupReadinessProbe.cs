using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.Startup;

/// <summary> Implements daemon startup endpoint probing via repeated ping attempts. </summary>
internal sealed class DaemonStartupReadinessProbe : IDaemonStartupReadinessProbe
{
    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IUnityLogReader unityLogReader;

    private readonly IUnityProjectLockPreflightService unityProjectLockPreflightService;

    private readonly DaemonCompensationOperationOwner compensationOperationOwner;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonStartupReadinessProbe" /> class. </summary>
    /// <param name="daemonPingInfoClient"> The daemon ping-info client dependency. </param>
    /// <param name="unityLogReader"> The Unity log-reader dependency. </param>
    /// <param name="unityProjectLockPreflightService"> The Unity project lock preflight service dependency. </param>
    /// <param name="compensationOperationOwner"> The owner for project-lock mutations that outlive the startup deadline. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting and retry delays. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStartupReadinessProbe (
        IDaemonPingInfoClient daemonPingInfoClient,
        IUnityLogReader unityLogReader,
        IUnityProjectLockPreflightService unityProjectLockPreflightService,
        DaemonCompensationOperationOwner compensationOperationOwner,
        TimeProvider timeProvider)
    {
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.unityLogReader = unityLogReader ?? throw new ArgumentNullException(nameof(unityLogReader));
        this.unityProjectLockPreflightService = unityProjectLockPreflightService ?? throw new ArgumentNullException(nameof(unityProjectLockPreflightService));
        this.compensationOperationOwner = compensationOperationOwner ?? throw new ArgumentNullException(nameof(compensationOperationOwner));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Waits until daemon startup accepts execution requests, or fails when timeout expires or startup reaches one non-waitable lifecycle state. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The startup readiness timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The readiness probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartupReadinessProbeResult> WaitUntilReadyAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        int? daemonProcessId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        if (daemonProcessId is int pid && pid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(daemonProcessId), daemonProcessId, "Daemon process id must be greater than zero.");
        }

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (daemonProcessId is int processId && !ProcessLivenessProbe.IsAlive(processId))
            {
                var postExitLockDiagnostic = await CreatePostExitLockDiagnosticAsync(
                        unityProject,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (postExitLockDiagnostic.DeadlineExpired)
                {
                    return DaemonStartupReadinessProbeResult.Failure(ExecutionError.InternalError(
                        $"Unity daemon process exited before startup readiness was confirmed. ProcessId={processId}."));
                }

                var startupFailure = await TryClassifyStartupFailureAsync(
                        unityProject,
                        includeProjectLockFile: false,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (startupFailure.Error is not null)
                {
                    return DaemonStartupReadinessProbeResult.Failure(
                        AppendDiagnostic(startupFailure.Error, postExitLockDiagnostic.Diagnostic),
                        startupFailure.Classification);
                }

                return DaemonStartupReadinessProbeResult.Failure(ExecutionError.InternalError(
                    AppendDiagnostic(
                        $"Unity daemon process exited before startup readiness was confirmed. ProcessId={processId}.",
                        postExitLockDiagnostic.Diagnostic)));
            }

            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return DaemonStartupReadinessProbeResult.Failure(ExecutionError.Timeout(
                    $"Timed out while waiting for daemon startup. Timeout={timeout.TotalMilliseconds:0}ms."));
            }

            var attemptTimeout = remainingTimeout < DaemonTimeouts.ProbeAttemptTimeoutCap
                ? remainingTimeout
                : DaemonTimeouts.ProbeAttemptTimeoutCap;
            try
            {
                var pingResponse = await daemonPingInfoClient.PingAndReadAsync(
                        unityProject,
                        attemptTimeout,
                        validateProjectFingerprint: true,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (!DaemonStartLifecycleSnapshot.TryCreate(pingResponse, out var lifecycleSnapshot, out var lifecycleError))
                {
                    return DaemonStartupReadinessProbeResult.Failure(lifecycleError!);
                }

                return DaemonStartupReadinessProbeResult.Ready(lifecycleSnapshot!);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException)
            {
                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return DaemonStartupReadinessProbeResult.Failure(ExecutionError.Timeout(
                        $"Timed out while waiting for daemon startup. Timeout={timeout.TotalMilliseconds:0}ms."));
                }

                await TimeProviderDelay.DelayAsync(GetRetryDelay(remainingTimeout), timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
            {
                // NOTE:
                // A Unity process launched by uCLI creates Temp/UnityLockfile before its IPC server
                // is ready. Treat that lock as external only when no launched process id is known.
                var startupFailure = await TryClassifyStartupFailureAsync(
                        unityProject,
                        includeProjectLockFile: daemonProcessId is null,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (startupFailure.DeadlineExpired)
                {
                    return DaemonStartupReadinessProbeResult.Failure(ExecutionError.Timeout(
                        $"Timed out while waiting for daemon startup. Timeout={timeout.TotalMilliseconds:0}ms."));
                }

                if (startupFailure.Error is not null)
                {
                    return DaemonStartupReadinessProbeResult.Failure(
                        startupFailure.Error,
                        startupFailure.Classification);
                }

                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return DaemonStartupReadinessProbeResult.Failure(ExecutionError.Timeout(
                        $"Timed out while waiting for daemon startup. Timeout={timeout.TotalMilliseconds:0}ms."));
                }

                await TimeProviderDelay.DelayAsync(GetRetryDelay(remainingTimeout), timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return DaemonStartupReadinessProbeResult.Failure(ExecutionError.InternalError(
                    $"Failed while probing daemon startup readiness. {exception.Message}"));
            }
        }
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }

    private async ValueTask<StartupFailureClassificationResult> TryClassifyStartupFailureAsync (
        ResolvedUnityProjectContext unityProject,
        bool includeProjectLockFile,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (includeProjectLockFile)
        {
            var projectLockPreflightExecution = await compensationOperationOwner.ExecuteAsync(
                    unityProject,
                    DaemonOperationLane.LifecycleCompensation,
                    deadline,
                    cancellationToken,
                    "Timed out before Unity project-lock preflight could begin.",
                    "Timed out while checking the Unity project lock during startup.",
                    (_, ownedCancellationToken) => unityProjectLockPreflightService.PrepareForUnityProcessStartAsync(
                        unityProject,
                        ownedCancellationToken))
                .ConfigureAwait(false);
            if (!projectLockPreflightExecution.IsSuccess)
            {
                return new StartupFailureClassificationResult(null, null, DeadlineExpired: true);
            }

            var projectLockPreflightResult = projectLockPreflightExecution.Value!;
            var projectLockError = UnityProjectLockPreflightErrorFactory.CreateLaunchBlockingError(
                unityProject,
                projectLockPreflightResult);
            if (projectLockError != null)
            {
                return new StartupFailureClassificationResult(projectLockError, null, DeadlineExpired: false);
            }
        }

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
            return new StartupFailureClassificationResult(null, null, DeadlineExpired: true);
        }

        var logReadResult = logReadOperation.Value!;
        if (!logReadResult.IsSuccess || string.IsNullOrWhiteSpace(logReadResult.Text))
        {
            return new StartupFailureClassificationResult(null, null, DeadlineExpired: false);
        }

        var latestStartupLogText = DaemonStartupFailureLogClassifier.GetLatestStartupLogText(logReadResult.Text);
        if (!DaemonStartupFailureLogClassifier.TryClassifyFailure(
                latestStartupLogText,
                DaemonStartupFailureClassificationContext.Batchmode,
                out var classification))
        {
            return new StartupFailureClassificationResult(null, null, DeadlineExpired: false);
        }

        return new StartupFailureClassificationResult(
            ExecutionError.InternalError(classification!.Message, DaemonErrorCodes.DaemonStartupBlocked),
            classification,
            DeadlineExpired: false);
    }

    private async ValueTask<PostExitLockDiagnosticResult> CreatePostExitLockDiagnosticAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var cleanupExecution = await compensationOperationOwner.ExecuteAsync(
                unityProject,
                DaemonOperationLane.LifecycleCompensation,
                deadline,
                cancellationToken,
                "Timed out before post-exit Unity project-lock cleanup could begin.",
                "Timed out while cleaning the Unity project lock after process exit.",
                (_, ownedCancellationToken) => unityProjectLockPreflightService.CleanupStaleLockAfterUnityProcessExitAsync(
                    unityProject,
                    ownedCancellationToken))
            .ConfigureAwait(false);
        if (!cleanupExecution.IsSuccess)
        {
            return new PostExitLockDiagnosticResult(null, DeadlineExpired: true);
        }

        return new PostExitLockDiagnosticResult(
            UnityProjectLockPreflightErrorFactory.CreatePostExitDiagnostic(cleanupExecution.Value!),
            DeadlineExpired: false);
    }

    private static ExecutionError AppendDiagnostic (
        ExecutionError error,
        string? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(error);
        return string.IsNullOrWhiteSpace(diagnostic)
            ? error
            : error with { Message = AppendDiagnostic(error.Message, diagnostic) };
    }

    private static string AppendDiagnostic (
        string message,
        string? diagnostic)
    {
        return string.IsNullOrWhiteSpace(diagnostic)
            ? message
            : $"{message} {diagnostic}";
    }

    private readonly record struct StartupFailureClassificationResult (
        ExecutionError? Error,
        DaemonStartupFailureClassification? Classification,
        bool DeadlineExpired);

    private readonly record struct PostExitLockDiagnosticResult (
        string? Diagnostic,
        bool DeadlineExpired);
}

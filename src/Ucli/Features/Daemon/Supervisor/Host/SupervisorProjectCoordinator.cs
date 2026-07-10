using System.Collections.Concurrent;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Reachability;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Coordinates per-project daemon lifecycle operations owned by the supervisor runtime. </summary>
internal sealed class SupervisorProjectCoordinator
{
    private const string StabilityCompensationLogMessage =
        "Queued background compensation stop after supervisor stability verification failed. " +
        "fingerprint={0}";

    private const string ProjectGateTimeoutMessage =
        "Timed out while waiting for the supervisor project gate.";

    private const string PendingOperationTimeoutMessage =
        "Timed out while waiting for prior supervisor lifecycle cleanup to finish.";

    private readonly IDaemonStartOperation daemonStartOperation;

    private readonly IDaemonStopOperation daemonStopOperation;

    private readonly IDaemonPingClient daemonPingClient;

    private readonly IDaemonReachabilityClassifier daemonReachabilityClassifier;

    private readonly SupervisorStabilityVerifier stabilityVerifier;

    private readonly SupervisorExitHandler exitHandler;

    private readonly SupervisorRuntimeLogger runtimeLogger;

    private readonly TimeProvider timeProvider;

    private readonly ConcurrentDictionary<string, SupervisorProjectSlot> projectSlots = new(StringComparer.Ordinal);

    private int managedProjectCount;

    private int pendingOperationCount;

    /// <summary> Initializes a new instance of the <see cref="SupervisorProjectCoordinator" /> class. </summary>
    /// <param name="daemonStartOperation"> The daemon start-operation dependency. </param>
    /// <param name="daemonStopOperation"> The daemon stop-operation dependency. </param>
    /// <param name="stabilityVerifier"> The stability-verifier dependency. </param>
    /// <param name="exitHandler"> The managed-process exit-handler dependency. </param>
    /// <param name="runtimeLogger"> The runtime logger dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    public SupervisorProjectCoordinator (
        IDaemonStartOperation daemonStartOperation,
        IDaemonStopOperation daemonStopOperation,
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier daemonReachabilityClassifier,
        SupervisorStabilityVerifier stabilityVerifier,
        SupervisorExitHandler exitHandler,
        SupervisorRuntimeLogger runtimeLogger,
        TimeProvider timeProvider)
    {
        this.daemonStartOperation = daemonStartOperation ?? throw new ArgumentNullException(nameof(daemonStartOperation));
        this.daemonStopOperation = daemonStopOperation ?? throw new ArgumentNullException(nameof(daemonStopOperation));
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.daemonReachabilityClassifier = daemonReachabilityClassifier ?? throw new ArgumentNullException(nameof(daemonReachabilityClassifier));
        this.stabilityVerifier = stabilityVerifier ?? throw new ArgumentNullException(nameof(stabilityVerifier));
        this.exitHandler = exitHandler ?? throw new ArgumentNullException(nameof(exitHandler));
        this.runtimeLogger = runtimeLogger ?? throw new ArgumentNullException(nameof(runtimeLogger));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Gets a value indicating whether one or more managed Unity daemon processes are currently tracked. </summary>
    public bool HasManagedProjects => Volatile.Read(ref managedProjectCount) > 0;

    /// <summary> Gets a value indicating whether one or more managed processes or pending lifecycle tasks exist. </summary>
    public bool HasActiveProjectWork => HasManagedProjects || Volatile.Read(ref pendingOperationCount) > 0;

    /// <summary> Ensures one Unity daemon is running for the specified project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The command timeout. </param>
    /// <param name="editorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="onStartupBlocked"> The startup-blocked process policy requested by the caller. </param>
    /// <param name="progressObserver"> The optional observer for supervisor-internal start progress. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The daemon-start result. </returns>
    public async ValueTask<DaemonStartResult> EnsureRunningAsync (
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

        var slot = GetOrCreateSlot(unityProject.ProjectFingerprint);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (!await TryEnterProjectGateAsync(slot, deadline, cancellationToken).ConfigureAwait(false))
        {
            return DaemonStartResult.Failure(ExecutionError.Timeout(ProjectGateTimeoutMessage));
        }

        try
        {
            var pendingOperationWaitError = await AwaitPendingOperationAsync(
                    slot,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (pendingOperationWaitError != null)
            {
                return DaemonStartResult.Failure(pendingOperationWaitError);
            }

            if (!deadline.TryGetRemainingTimeout(out var daemonStartTimeout))
            {
                return DaemonStartResult.Failure(ExecutionError.Timeout(
                    "Timed out before supervisor start work could begin."));
            }

            var startResult = await daemonStartOperation.StartAsync(
                    unityProject,
                    daemonStartTimeout,
                    editorMode,
                    onStartupBlocked,
                    progressObserver,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!startResult.IsSuccess)
            {
                return startResult;
            }

            if (startResult.Status == DaemonStartStatus.AlreadyRunning)
            {
                _ = TryRegisterManagedProcess(slot, unityProject, startResult.Session!);
                return DaemonStartResult.AlreadyRunning(startResult.Session!, startResult.LifecycleSnapshot);
            }

            if (startResult.Status == DaemonStartStatus.Attached)
            {
                return DaemonStartResult.Attached(startResult.Session!, startResult.LifecycleSnapshot);
            }

            // NOTE:
            // Register manageable launched daemons before stability verification so cancellation or
            // verification failure cannot leave a supervisor-owned process outside supervisor ownership.
            if (!TryRegisterManagedProcess(slot, unityProject, startResult.Session!))
            {
                return DaemonStartResult.Started(startResult.Session!, startResult.LifecycleSnapshot);
            }

            if (!deadline.TryGetRemainingTimeout(out var stabilityTimeout))
            {
                ScheduleBackgroundCompensationStop(slot, unityProject);
                return DaemonStartResult.Failure(ExecutionError.Timeout(
                    "Timed out before supervisor stability verification could begin."));
            }

            SupervisorStabilityVerificationResult stabilityResult;
            try
            {
                stabilityResult = await stabilityVerifier.EnsureStableAsync(
                        unityProject,
                        startResult.Session!,
                        stabilityTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                ScheduleBackgroundCompensationStop(slot, unityProject);
                throw;
            }

            if (!stabilityResult.IsSuccess)
            {
                ScheduleBackgroundCompensationStop(slot, unityProject);
                return DaemonStartResult.Failure(stabilityResult.Error!);
            }

            return DaemonStartResult.Started(startResult.Session!, startResult.LifecycleSnapshot);
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    /// <summary> Stops one Unity daemon for the specified project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The command timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The daemon-stop result. </returns>
    public async ValueTask<DaemonStopResult> StopProjectAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var slot = GetOrCreateSlot(unityProject.ProjectFingerprint);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (!await TryEnterProjectGateAsync(slot, deadline, cancellationToken).ConfigureAwait(false))
        {
            return DaemonStopResult.Failure(ExecutionError.Timeout(ProjectGateTimeoutMessage));
        }

        try
        {
            var pendingOperationWaitError = await AwaitPendingOperationAsync(
                    slot,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (pendingOperationWaitError != null)
            {
                return DaemonStopResult.Failure(pendingOperationWaitError);
            }

            if (!deadline.TryGetRemainingTimeout(out var stopTimeout))
            {
                return DaemonStopResult.Failure(ExecutionError.Timeout(
                    "Timed out before supervisor stop work could begin."));
            }

            var managedProcess = slot.ManagedProcess;
            managedProcess?.BeginStopRequest();
            DaemonStopResult stopResult;
            try
            {
                stopResult = await daemonStopOperation.StopAsync(
                        unityProject,
                        stopTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                managedProcess?.CompleteStopRequest(false);
                throw;
            }

            managedProcess?.CompleteStopRequest(stopResult.IsSuccess);
            if (stopResult.IsSuccess)
            {
                ClearManagedProcess(slot);
            }

            return stopResult;
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    /// <summary> Awaits all currently tracked monitor tasks. </summary>
    public async Task AwaitManagedProcessesAsync ()
    {
        while (true)
        {
            var trackedTasks = GetTrackedTasks();
            if (trackedTasks.Length == 0)
            {
                if (!HasActiveProjectWork)
                {
                    return;
                }

                await Task.Yield();
                continue;
            }

            try
            {
                await Task.WhenAll(trackedTasks).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // NOTE:
                // shutdown should continue even when a detached monitor task already observed an exit race.
            }

            DetachCompletedManagedProcesses();

            if (!HasActiveProjectWork && GetTrackedTasks().Length == 0)
            {
                return;
            }

            await Task.Yield();
        }
    }

    private SupervisorProjectSlot GetOrCreateSlot (string projectFingerprint)
    {
        return projectSlots.GetOrAdd(projectFingerprint, static _ => new SupervisorProjectSlot());
    }

    private bool TryRegisterManagedProcess (
        SupervisorProjectSlot slot,
        ResolvedUnityProjectContext unityProject,
        DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);

        if (!DaemonSessionTerminationPolicy.CanShutdownProcess(session))
        {
            return false;
        }

        if (slot.ManagedProcess != null
            && IsSameManagedProcess(slot.ManagedProcess, session))
        {
            return true;
        }

        var managedProcess = CreateManagedProcess(unityProject, session);
        if (slot.ManagedProcess == null)
        {
            Interlocked.Increment(ref managedProjectCount);
        }
        else
        {
            slot.ManagedProcess.StopMonitoring();
        }

        slot.ManagedProcess = managedProcess;
        return true;
    }

    private async Task HandleManagedProcessExitAsync (SupervisorManagedDaemonProcess managedProcess)
    {
        ArgumentNullException.ThrowIfNull(managedProcess);

        var slot = GetOrCreateSlot(managedProcess.UnityProject.ProjectFingerprint);
        await slot.Gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (!ReferenceEquals(slot.ManagedProcess, managedProcess))
            {
                return;
            }

            await exitHandler.HandleExitAsync(managedProcess, CancellationToken.None).ConfigureAwait(false);
            ClearManagedProcess(slot);
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    private void ClearManagedProcess (SupervisorProjectSlot slot)
    {
        if (slot.ManagedProcess == null)
        {
            return;
        }

        slot.ManagedProcess.StopMonitoring();
        slot.ManagedProcess = null;
        Interlocked.Decrement(ref managedProjectCount);
    }

    private SupervisorManagedDaemonProcess CreateManagedProcess (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);

        if (session.ProcessId is int processId && processId > 0)
        {
            return new SupervisorManagedDaemonProcess(
                unityProject,
                session,
                processId,
                HandleManagedProcessExitAsync);
        }

        return new SupervisorManagedDaemonProcess(
            unityProject,
            session,
            daemonPingClient,
            daemonReachabilityClassifier,
            HandleManagedProcessExitAsync);
    }

    private static bool IsSameManagedProcess (
        SupervisorManagedDaemonProcess managedProcess,
        DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(managedProcess);
        ArgumentNullException.ThrowIfNull(session);
        return SupervisorSessionIdentity.IsSameSession(managedProcess.Session, session)
            && managedProcess.ProcessId == session.ProcessId;
    }

    private static async ValueTask<bool> TryEnterProjectGateAsync (
        SupervisorProjectSlot slot,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slot);
        cancellationToken.ThrowIfCancellationRequested();

        if (!deadline.TryGetRemainingTimeout(out var gateTimeout))
        {
            return false;
        }

        return await slot.Gate.WaitAsync(gateTimeout, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<ExecutionError?> AwaitPendingOperationAsync (
        SupervisorProjectSlot slot,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slot);

        var pendingOperation = slot.PendingOperation;
        if (pendingOperation == null)
        {
            return null;
        }

        if (!deadline.TryGetRemainingTimeout(out var pendingOperationTimeout))
        {
            return ExecutionError.Timeout(PendingOperationTimeoutMessage);
        }

        try
        {
            await pendingOperation.WaitAsync(pendingOperationTimeout, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (TimeoutException)
        {
            return ExecutionError.Timeout(PendingOperationTimeoutMessage);
        }
    }

    private void ScheduleBackgroundCompensationStop (
        SupervisorProjectSlot slot,
        ResolvedUnityProjectContext unityProject)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(unityProject);

        if (slot.ManagedProcess == null || slot.PendingOperation != null)
        {
            return;
        }

        Interlocked.Increment(ref pendingOperationCount);
        var startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        slot.PendingOperation = RunBackgroundCompensationStopAsync(
            startSignal.Task,
            slot,
            unityProject,
            slot.ManagedProcess);
        startSignal.TrySetResult();
    }

    private async Task RunBackgroundCompensationStopAsync (
        Task startSignal,
        SupervisorProjectSlot slot,
        ResolvedUnityProjectContext unityProject,
        SupervisorManagedDaemonProcess managedProcess)
    {
        ArgumentNullException.ThrowIfNull(startSignal);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(managedProcess);

        await startSignal.ConfigureAwait(false);
        managedProcess.BeginStopRequest();
        var queuedLogTask = WriteRuntimeLogBestEffortAsync(
            unityProject.RepositoryRoot,
            "warning",
            string.Format(
                StabilityCompensationLogMessage,
                unityProject.ProjectFingerprint));
        try
        {
            var stopResult = await daemonStopOperation.StopAsync(
                    unityProject,
                    DaemonTimeouts.StopCompensationTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false);
            managedProcess.CompleteStopRequest(stopResult.IsSuccess);
            await queuedLogTask.ConfigureAwait(false);
            if (!stopResult.IsSuccess)
            {
                await WriteRuntimeLogBestEffortAsync(
                        unityProject.RepositoryRoot,
                        "error",
                        $"Background compensation stop failed. fingerprint={unityProject.ProjectFingerprint} error={stopResult.Error!.Message}")
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            managedProcess.CompleteStopRequest(false);
            await queuedLogTask.ConfigureAwait(false);
            await WriteRuntimeLogBestEffortAsync(
                    unityProject.RepositoryRoot,
                    "error",
                    $"Background compensation stop crashed. fingerprint={unityProject.ProjectFingerprint} {exception}")
                .ConfigureAwait(false);
        }
        finally
        {
            slot.PendingOperation = null;
            Interlocked.Decrement(ref pendingOperationCount);
        }
    }

    private async Task WriteRuntimeLogBestEffortAsync (
        string storageRoot,
        string level,
        string message)
    {
        try
        {
            var deadline = ExecutionDeadline.Start(
                SupervisorConstants.RuntimeLogWriteTimeout,
                timeProvider);
            _ = await ExecutionDeadlineOperation.ExecuteAsync(
                    deadline,
                    CancellationToken.None,
                    "Timed out before the supervisor runtime-log write could begin.",
                    "Timed out while writing the supervisor runtime log.",
                    async operationCancellationToken =>
                    {
                        await runtimeLogger.WriteAsync(
                                storageRoot,
                                level,
                                message,
                                operationCancellationToken)
                            .ConfigureAwait(false);
                        return true;
                    })
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Runtime logging is supplemental and must not delay or fail required compensation.
        }
    }

    private static IEnumerable<Task?> EnumerateTrackedTasks (SupervisorProjectSlot slot)
    {
        yield return slot.ManagedProcess?.MonitorTask;
        yield return slot.PendingOperation;
    }

    private void DetachCompletedManagedProcesses ()
    {
        foreach (var slot in projectSlots.Values)
        {
            if (!slot.Gate.Wait(0))
            {
                continue;
            }

            try
            {
                var managedProcess = slot.ManagedProcess;
                if (managedProcess == null
                    || !managedProcess.MonitorTask.IsCompleted)
                {
                    continue;
                }

                // NOTE:
                // shutdown should stop tracking already-completed monitor tasks, even when
                // exit cleanup faulted, so AwaitManagedProcesses does not replay the same
                // fault forever while waiting for managed-project bookkeeping to drain.
                ClearManagedProcess(slot);
            }
            finally
            {
                slot.Gate.Release();
            }
        }
    }

    private Task[] GetTrackedTasks ()
    {
        return projectSlots.Values
            .SelectMany(static x => EnumerateTrackedTasks(x))
            .Where(static x => x != null)
            .Cast<Task>()
            .ToArray();
    }
}

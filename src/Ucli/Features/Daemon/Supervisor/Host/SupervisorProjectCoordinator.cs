using System.Collections.Concurrent;
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
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;

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
        TimeProvider? timeProvider = null)
    {
        this.daemonStartOperation = daemonStartOperation ?? throw new ArgumentNullException(nameof(daemonStartOperation));
        this.daemonStopOperation = daemonStopOperation ?? throw new ArgumentNullException(nameof(daemonStopOperation));
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.daemonReachabilityClassifier = daemonReachabilityClassifier ?? throw new ArgumentNullException(nameof(daemonReachabilityClassifier));
        this.stabilityVerifier = stabilityVerifier ?? throw new ArgumentNullException(nameof(stabilityVerifier));
        this.exitHandler = exitHandler ?? throw new ArgumentNullException(nameof(exitHandler));
        this.runtimeLogger = runtimeLogger ?? throw new ArgumentNullException(nameof(runtimeLogger));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Gets a value indicating whether one or more managed Unity daemon processes are currently tracked. </summary>
    public bool HasManagedProjects => Volatile.Read(ref managedProjectCount) > 0;

    /// <summary> Gets a value indicating whether one or more managed processes or pending lifecycle tasks exist. </summary>
    public bool HasActiveProjectWork => HasManagedProjects || Volatile.Read(ref pendingOperationCount) > 0;

    /// <summary> Ensures one Unity daemon is running for the specified project. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The command timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The daemon-start result. </returns>
    public async ValueTask<DaemonStartResult> EnsureRunning (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var slot = GetOrCreateSlot(unityProject.ProjectFingerprint);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (!await TryEnterProjectGate(slot, deadline, cancellationToken).ConfigureAwait(false))
        {
            return DaemonStartResult.Failure(ExecutionError.Timeout(ProjectGateTimeoutMessage));
        }

        try
        {
            var pendingOperationWaitError = await AwaitPendingOperation(
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

            var startResult = await daemonStartOperation.Start(
                    unityProject,
                    daemonStartTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!startResult.IsSuccess)
            {
                return startResult;
            }

            if (startResult.Status == DaemonStartStatus.AlreadyRunning)
            {
                RegisterManagedProcess(slot, unityProject, startResult.Session!);
                return DaemonStartResult.AlreadyRunning(startResult.Session!);
            }

            // NOTE:
            // register the launched daemon before stability verification so cancellation or
            // verification failure cannot leave a started process outside supervisor ownership.
            RegisterManagedProcess(slot, unityProject, startResult.Session!);

            if (!deadline.TryGetRemainingTimeout(out var stabilityTimeout))
            {
                ScheduleBackgroundCompensationStop(slot, unityProject);
                return DaemonStartResult.Failure(ExecutionError.Timeout(
                    "Timed out before supervisor stability verification could begin."));
            }

            SupervisorStabilityVerificationResult stabilityResult;
            try
            {
                stabilityResult = await stabilityVerifier.EnsureStable(
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

            return DaemonStartResult.Started(startResult.Session!);
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
    public async ValueTask<DaemonStopResult> StopProject (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var slot = GetOrCreateSlot(unityProject.ProjectFingerprint);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (!await TryEnterProjectGate(slot, deadline, cancellationToken).ConfigureAwait(false))
        {
            return DaemonStopResult.Failure(ExecutionError.Timeout(ProjectGateTimeoutMessage));
        }

        try
        {
            var pendingOperationWaitError = await AwaitPendingOperation(
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
                stopResult = await daemonStopOperation.Stop(
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
    public async Task AwaitManagedProcesses ()
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

    private void RegisterManagedProcess (
        SupervisorProjectSlot slot,
        ResolvedUnityProjectContext unityProject,
        DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);

        if (slot.ManagedProcess != null
            && IsSameManagedProcess(slot.ManagedProcess, session))
        {
            return;
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
    }

    private async Task HandleManagedProcessExit (SupervisorManagedDaemonProcess managedProcess)
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

            await exitHandler.HandleExit(managedProcess, CancellationToken.None).ConfigureAwait(false);
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
                HandleManagedProcessExit);
        }

        return new SupervisorManagedDaemonProcess(
            unityProject,
            session,
            daemonPingClient,
            daemonReachabilityClassifier,
            HandleManagedProcessExit);
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

    private static async ValueTask<bool> TryEnterProjectGate (
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

    private static async ValueTask<ExecutionError?> AwaitPendingOperation (
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
        slot.PendingOperation = RunBackgroundCompensationStop(slot, unityProject, slot.ManagedProcess);
    }

    private async Task RunBackgroundCompensationStop (
        SupervisorProjectSlot slot,
        ResolvedUnityProjectContext unityProject,
        SupervisorManagedDaemonProcess managedProcess)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(managedProcess);

        managedProcess.BeginStopRequest();
        try
        {
            await runtimeLogger.Write(
                    unityProject.RepositoryRoot,
                    "warning",
                    string.Format(
                        StabilityCompensationLogMessage,
                        unityProject.ProjectFingerprint),
                    CancellationToken.None)
                .ConfigureAwait(false);

            var stopResult = await daemonStopOperation.Stop(
                    unityProject,
                    DaemonTimeouts.StopCompensationTimeout,
                    CancellationToken.None)
                .ConfigureAwait(false);
            managedProcess.CompleteStopRequest(stopResult.IsSuccess);
            if (!stopResult.IsSuccess)
            {
                await runtimeLogger.Write(
                        unityProject.RepositoryRoot,
                        "error",
                        $"Background compensation stop failed. fingerprint={unityProject.ProjectFingerprint} error={stopResult.Error!.Message}",
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            managedProcess.CompleteStopRequest(false);
            await runtimeLogger.Write(
                    unityProject.RepositoryRoot,
                    "error",
                    $"Background compensation stop crashed. fingerprint={unityProject.ProjectFingerprint} {exception}",
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        finally
        {
            slot.PendingOperation = null;
            Interlocked.Decrement(ref pendingOperationCount);
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

    private static ExecutionError CreateAugmentedPrimaryError (
        ExecutionError primaryError,
        ExecutionError? compensationError)
    {
        ArgumentNullException.ThrowIfNull(primaryError);
        ArgumentNullException.ThrowIfNull(compensationError);

        var message =
            "Supervisor compensation stop failed after daemon startup had already succeeded. " +
            $"PrimaryError={primaryError.Message} " +
            $"CompensationError={compensationError.Message}";

        return primaryError.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => ExecutionError.InvalidArgument(message),
            ExecutionErrorKind.Timeout => ExecutionError.Timeout(message),
            ExecutionErrorKind.InternalError => ExecutionError.InternalError(message),
            _ => throw new ArgumentOutOfRangeException(nameof(primaryError), primaryError.Kind, "Unsupported execution error kind."),
        };
    }
}
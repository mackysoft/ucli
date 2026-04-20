using System.Diagnostics;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Tracks one supervisor-owned Unity daemon process and notifies when it exits. </summary>
internal sealed class SupervisorManagedDaemonProcess
{
    private const int StopStateNone = 0;

    private const int StopStateInProgress = 1;

    private const int StopStateSucceeded = 2;

    private int stopState;

    private readonly CancellationTokenSource monitorCancellationTokenSource = new();

    /// <summary> Initializes a new instance of the <see cref="SupervisorManagedDaemonProcess" /> class. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The tracked daemon session. </param>
    /// <param name="processId"> The tracked daemon process identifier. </param>
    /// <param name="onExited"> The callback invoked after process exit is observed. </param>
    public SupervisorManagedDaemonProcess (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        int processId,
        Func<SupervisorManagedDaemonProcess, Task> onExited)
    {
        UnityProject = unityProject ?? throw new ArgumentNullException(nameof(unityProject));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        ProcessId = processId;
        MonitorTask = MonitorProcessById(
            onExited ?? throw new ArgumentNullException(nameof(onExited)),
            monitorCancellationTokenSource.Token);
    }

    /// <summary> Initializes a new instance of the <see cref="SupervisorManagedDaemonProcess" /> class for a pid-less managed daemon session. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The tracked daemon session. </param>
    /// <param name="daemonPingClient"> The daemon ping-client dependency used for reachability monitoring. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability-classifier dependency. </param>
    /// <param name="onExited"> The callback invoked after daemon exit is observed. </param>
    public SupervisorManagedDaemonProcess (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        Func<SupervisorManagedDaemonProcess, Task> onExited)
    {
        UnityProject = unityProject ?? throw new ArgumentNullException(nameof(unityProject));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        ProcessId = session.ProcessId;
        MonitorTask = MonitorProcessByPing(
            daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient)),
            reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier)),
            onExited ?? throw new ArgumentNullException(nameof(onExited)),
            monitorCancellationTokenSource.Token);
    }

    /// <summary> Gets the resolved Unity project context owned by this managed process. </summary>
    public ResolvedUnityProjectContext UnityProject { get; }

    /// <summary> Gets the tracked daemon session snapshot. </summary>
    public DaemonSession Session { get; }

    /// <summary> Gets the tracked Unity daemon process identifier. </summary>
    public int? ProcessId { get; }

    /// <summary> Gets a value indicating whether stop was explicitly requested by the supervisor. </summary>
    public bool IsStopRequested => Volatile.Read(ref stopState) != StopStateNone;

    /// <summary> Gets the background monitor task that completes after process exit handling finishes. </summary>
    public Task MonitorTask { get; }

    /// <summary> Marks the managed process as being targeted by an explicit stop attempt. </summary>
    public void BeginStopRequest ()
    {
        Interlocked.CompareExchange(ref stopState, StopStateInProgress, StopStateNone);
    }

    /// <summary> Finalizes the explicit stop attempt outcome. </summary>
    /// <param name="succeeded"> <see langword="true" /> when the stop operation succeeded; otherwise <see langword="false" />. </param>
    public void CompleteStopRequest (bool succeeded)
    {
        if (succeeded)
        {
            Interlocked.Exchange(ref stopState, StopStateSucceeded);
            return;
        }

        Interlocked.CompareExchange(ref stopState, StopStateNone, StopStateInProgress);
    }

    /// <summary> Stops the background monitor without invoking the exit callback. </summary>
    public void StopMonitoring ()
    {
        monitorCancellationTokenSource.Cancel();
    }

    private async Task MonitorProcessById (
        Func<SupervisorManagedDaemonProcess, Task> onExited,
        CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                using var process = Process.GetProcessById(ProcessId!.Value);
                try
                {
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (InvalidOperationException)
                {
                }
            }
            catch (ArgumentException)
            {
            }
        }
        finally
        {
            await onExited(this).ConfigureAwait(false);
        }
    }

    private async Task MonitorProcessByPing (
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        Func<SupervisorManagedDaemonProcess, Task> onExited,
        CancellationToken cancellationToken)
    {
        try
        {
            // NOTE:
            // pid-less sessions can still represent a live daemon after attach.
            // Poll reachability by session token so supervisor ownership is not dropped
            // just because processId was unavailable in the persisted session snapshot.
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await daemonPingClient.Ping(
                            UnityProject,
                            SupervisorConstants.PingTimeout,
                            Session.SessionToken,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (TimeoutException)
                {
                }
                catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
                {
                    break;
                }
                catch (Exception)
                {
                }

                await Task.Delay(SupervisorConstants.PidlessMonitorPollDelay, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await onExited(this).ConfigureAwait(false);
    }
}
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Shared.Unity.Process;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process;

/// <summary> Implements process termination checks and force-kill fallback by process identifier. </summary>
internal sealed class DaemonProcessTerminationService : IDaemonProcessTerminationService
{
    private static readonly TimeSpan PassiveExitWaitTimeoutCap = TimeSpan.FromSeconds(1);

    private readonly DaemonProcessIdentityAssessor daemonProcessIdentityAssessor;

    /// <summary> Initializes a new instance of the <see cref="DaemonProcessTerminationService" /> class. </summary>
    /// <param name="daemonProcessIdentityAssessor"> The daemon process-identity assessor dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonProcessIdentityAssessor" /> is <see langword="null" />. </exception>
    public DaemonProcessTerminationService (DaemonProcessIdentityAssessor daemonProcessIdentityAssessor)
    {
        this.daemonProcessIdentityAssessor = daemonProcessIdentityAssessor ?? throw new ArgumentNullException(nameof(daemonProcessIdentityAssessor));
    }

    /// <summary> Ensures daemon process is stopped before timeout expires. </summary>
    /// <param name="processId"> The daemon process identifier when available. </param>
    /// <param name="expectedIssuedAtUtc"> The expected daemon-session issuance timestamp used for process identity verification. </param>
    /// <param name="timeout"> The process termination timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The process termination result. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonSessionStoreOperationResult> EnsureStopped (
        int? processId,
        DateTimeOffset? expectedIssuedAtUtc,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        if (processId is null)
        {
            return DaemonSessionStoreOperationResult.Success();
        }

        DiagnosticsProcess process;
        try
        {
            process = DiagnosticsProcess.GetProcessById(processId.Value);
        }
        catch (ArgumentException)
        {
            return DaemonSessionStoreOperationResult.Success();
        }

        using (process)
        {
            if (expectedIssuedAtUtc is null)
            {
                return DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError(
                    $"Daemon process identity could not be verified because expected issuedAtUtc is not available for process '{processId.Value}'."));
            }

            var identityAssessment = daemonProcessIdentityAssessor.AssessProcess(process, processId.Value, expectedIssuedAtUtc.Value);
            switch (identityAssessment.Status)
            {
                case DaemonProcessIdentityAssessmentStatus.NotRunning:
                    return DaemonSessionStoreOperationResult.Success();

                case DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess:
                    break;

                case DaemonProcessIdentityAssessmentStatus.DifferentProcess:
                case DaemonProcessIdentityAssessmentStatus.Uncertain:
                    return DaemonSessionStoreOperationResult.Failure(identityAssessment.Error ?? ExecutionError.InternalError(
                        $"Daemon process identity could not be verified for process '{processId.Value}'."));

                default:
                    throw new ArgumentOutOfRangeException(nameof(identityAssessment), identityAssessment.Status, "Unsupported daemon process identity assessment status.");
            }

            var deadline = ExecutionDeadline.Start(timeout);
            // NOTE: Stop may have already sent a shutdown IPC request; give Unity a short chance to exit normally first.
            var passiveExitWaitTimeout = GetPassiveExitWaitTimeout(timeout);
            if (passiveExitWaitTimeout > TimeSpan.Zero
                && await WaitUntilExitedAsync(process, passiveExitWaitTimeout, cancellationToken).ConfigureAwait(false))
            {
                return DaemonSessionStoreOperationResult.Success();
            }

            if (!deadline.TryGetRemainingTimeout(out var terminationTimeout))
            {
                return DaemonSessionStoreOperationResult.Failure(ExecutionError.Timeout(
                    $"Timed out while waiting for daemon process '{processId.Value}' to stop."));
            }

            var terminationResult = await ProcessTerminator.TerminateAsync(
                    process,
                    CreateBoundedUnityTerminationPolicy(terminationTimeout),
                    cancellationToken)
                .ConfigureAwait(false);

            return terminationResult == ProcessTerminationResult.ForceKillFailed
                ? DaemonSessionStoreOperationResult.Failure(ExecutionError.Timeout(
                    $"Timed out while force-stopping daemon process '{processId.Value}'."))
                : DaemonSessionStoreOperationResult.Success();
        }
    }

    /// <summary> Creates a Unity termination policy bounded by the remaining daemon-stop budget. </summary>
    /// <param name="timeout"> The remaining timeout budget. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <returns> The default Unity policy when budget allows it; otherwise a proportional policy with at least one millisecond per phase. </returns>
    private static ProcessTerminationPolicy CreateBoundedUnityTerminationPolicy (TimeSpan timeout)
    {
        var defaultPolicy = UnityProcessTerminationPolicy.GracefulThenKill;
        if (timeout >= defaultPolicy.GraceTimeout + defaultPolicy.ForceKillWaitTimeout)
        {
            return defaultPolicy;
        }

        var totalMilliseconds = Math.Max(2, (int)Math.Ceiling(timeout.TotalMilliseconds));
        var graceMilliseconds = Math.Max(1, totalMilliseconds / 2);
        var forceKillMilliseconds = Math.Max(1, totalMilliseconds - graceMilliseconds);
        return new ProcessTerminationPolicy(
            ProcessTerminationMode.GracefulThenKill,
            TimeSpan.FromMilliseconds(graceMilliseconds),
            TimeSpan.FromMilliseconds(forceKillMilliseconds));
    }

    /// <summary> Calculates the passive wait budget used before sending a termination request. </summary>
    /// <param name="timeout"> The total process-stop timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <returns> A bounded passive wait duration, or <see cref="TimeSpan.Zero" /> when the budget is too small. </returns>
    private static TimeSpan GetPassiveExitWaitTimeout (TimeSpan timeout)
    {
        var totalMilliseconds = Math.Ceiling(timeout.TotalMilliseconds);
        if (totalMilliseconds <= 2)
        {
            return TimeSpan.Zero;
        }

        var passiveExitWaitMilliseconds = Math.Min(
            PassiveExitWaitTimeoutCap.TotalMilliseconds,
            totalMilliseconds - 2);
        return TimeSpan.FromMilliseconds(passiveExitWaitMilliseconds);
    }

    /// <summary> Waits until process exit is observed or the supplied timeout expires. </summary>
    /// <param name="process"> The process to observe. </param>
    /// <param name="timeout"> The maximum wait time. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> <see langword="true" /> when the process exit is observed; otherwise <see langword="false" />. </returns>
    private static async ValueTask<bool> WaitUntilExitedAsync (
        DiagnosticsProcess process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (HasExited(process))
        {
            return true;
        }

        using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellationTokenSource.Token);
        try
        {
            await process.WaitForExitAsync(linkedCancellationTokenSource.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCancellationTokenSource.IsCancellationRequested)
        {
            return HasExited(process);
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    /// <summary> Reads process exit state while treating post-exit access races as exited. </summary>
    /// <param name="process"> The process to inspect. </param>
    /// <returns> <see langword="true" /> when the process has exited or can no longer be inspected; otherwise <see langword="false" />. </returns>
    private static bool HasExited (DiagnosticsProcess process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
}

using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process;

/// <summary> Implements process termination checks and force-kill fallback by process identifier. </summary>
internal sealed class DaemonProcessTerminationService : IDaemonProcessTerminationService
{
    private readonly IDaemonProcessIdentityAssessor daemonProcessIdentityAssessor;

    /// <summary> Initializes a new instance of the <see cref="DaemonProcessTerminationService" /> class. </summary>
    /// <param name="daemonProcessIdentityAssessor"> The daemon process-identity assessor dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonProcessIdentityAssessor" /> is <see langword="null" />. </exception>
    public DaemonProcessTerminationService (IDaemonProcessIdentityAssessor daemonProcessIdentityAssessor)
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
            if (await WaitUntilExited(process, deadline, cancellationToken).ConfigureAwait(false))
            {
                return DaemonSessionStoreOperationResult.Success();
            }

            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception exception)
            {
                return DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError(
                    $"Failed to force-stop daemon process '{processId.Value}'. {exception.Message}"));
            }

            return await WaitUntilExited(process, deadline, cancellationToken).ConfigureAwait(false)
                ? DaemonSessionStoreOperationResult.Success()
                : DaemonSessionStoreOperationResult.Failure(ExecutionError.Timeout(
                    $"Timed out while force-stopping daemon process '{processId.Value}'."));
        }
    }

    /// <summary> Waits asynchronously until the target process exits or deadline elapses. </summary>
    /// <param name="process"> The target process instance. </param>
    /// <param name="deadline"> The shared timeout deadline. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> <see langword="true" /> when process exited; otherwise <see langword="false" /> when deadline elapsed. </returns>
    private static async ValueTask<bool> WaitUntilExited (
        DiagnosticsProcess process,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (HasExited(process))
            {
                return true;
            }

            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return false;
            }

            var retryDelayMilliseconds = Math.Min(
                DaemonTimeouts.ProcessTerminationProbeRetryDelayMilliseconds,
                Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
            await Task.Delay(retryDelayMilliseconds, cancellationToken).ConfigureAwait(false);

            try
            {
                process.Refresh();
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }

    /// <summary> Gets whether target process is already exited while tolerating post-exit access races. </summary>
    /// <param name="process"> The target process instance. </param>
    /// <returns> <see langword="true" /> when process already exited; otherwise <see langword="false" />. </returns>
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
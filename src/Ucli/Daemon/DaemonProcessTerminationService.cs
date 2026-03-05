using System.Diagnostics;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements process termination checks and force-kill fallback by process identifier. </summary>
internal sealed class DaemonProcessTerminationService : IDaemonProcessTerminationService
{
    private static readonly TimeSpan MaximumProcessStartLag = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan AllowedProcessStartLead = TimeSpan.FromSeconds(2);

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

        Process process;
        try
        {
            process = Process.GetProcessById(processId.Value);
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

            if (!TryValidateProcessIdentity(process, processId.Value, expectedIssuedAtUtc.Value, out var identityError))
            {
                return DaemonSessionStoreOperationResult.Failure(identityError!);
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
        Process process,
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

    /// <summary> Validates whether the target process identity matches expected daemon session issuance timing. </summary>
    /// <param name="process"> The process instance resolved by PID. </param>
    /// <param name="processId"> The process identifier used for diagnostics. </param>
    /// <param name="expectedIssuedAtUtc"> The expected daemon session issuance timestamp. </param>
    /// <param name="error"> The error when identity validation fails; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when identity validation succeeds; otherwise <see langword="false" />. </returns>
    private static bool TryValidateProcessIdentity (
        Process process,
        int processId,
        DateTimeOffset expectedIssuedAtUtc,
        out ExecutionError? error)
    {
        if (HasExited(process))
        {
            error = null;
            return true;
        }

        DateTimeOffset processStartTimeUtc;
        try
        {
            processStartTimeUtc = process.StartTime.ToUniversalTime();
        }
        catch (InvalidOperationException) when (HasExited(process))
        {
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = ExecutionError.InternalError(
                $"Failed to validate daemon process identity for process '{processId}'. {exception.Message}");
            return false;
        }

        var earliestAllowedStartTime = expectedIssuedAtUtc - AllowedProcessStartLead;
        var latestAllowedStartTime = expectedIssuedAtUtc + MaximumProcessStartLag;
        if (processStartTimeUtc < earliestAllowedStartTime || processStartTimeUtc > latestAllowedStartTime)
        {
            error = ExecutionError.InternalError(
                $"Daemon process identity mismatch for process '{processId}'. " +
                $"ExpectedStartRange=[{earliestAllowedStartTime:O}, {latestAllowedStartTime:O}] ActualStart={processStartTimeUtc:O}.");
            return false;
        }

        error = null;
        return true;
    }

    /// <summary> Gets whether target process is already exited while tolerating post-exit access races. </summary>
    /// <param name="process"> The target process instance. </param>
    /// <returns> <see langword="true" /> when process already exited; otherwise <see langword="false" />. </returns>
    private static bool HasExited (Process process)
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
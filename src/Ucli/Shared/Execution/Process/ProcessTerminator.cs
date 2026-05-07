using System.Runtime.InteropServices;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace MackySoft.Ucli.Shared.Execution.Process;

/// <summary> Terminates external processes according to one explicit termination policy. </summary>
internal static class ProcessTerminator
{
    private const int SigTerm = 15;

    /// <summary> Terminates one process according to <paramref name="policy" />. </summary>
    /// <param name="process"> The process to terminate. </param>
    /// <param name="policy"> The termination policy. When omitted, force kill is used. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The termination result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="process" /> is <see langword="null" />. </exception>
    public static async Task<ProcessTerminationResult> TerminateAsync (
        DiagnosticsProcess process,
        ProcessTerminationPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(process);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedPolicy = policy ?? ProcessTerminationPolicy.ForceKill;
        if (HasExited(process))
        {
            return ProcessTerminationResult.None;
        }

        if (resolvedPolicy.Mode == ProcessTerminationMode.ForceKill)
        {
            return await ForceKillAndWaitAsync(process, resolvedPolicy.ForceKillWaitTimeout, cancellationToken)
                .ConfigureAwait(false);
        }

        if (TryRequestGracefulExit(process)
            && await WaitUntilExitedAsync(process, resolvedPolicy.GraceTimeout, cancellationToken).ConfigureAwait(false))
        {
            return ProcessTerminationResult.GracefulExited;
        }

        return await ForceKillAndWaitAsync(process, resolvedPolicy.ForceKillWaitTimeout, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Requests non-destructive process exit when the platform exposes such a request. </summary>
    /// <param name="process"> The process that has not yet exited. </param>
    /// <returns> <see langword="true" /> when the graceful request was sent; otherwise <see langword="false" />. </returns>
    private static bool TryRequestGracefulExit (DiagnosticsProcess process)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // NOTE: Windows has no SIGTERM equivalent for no-window Unity batchmode processes; false falls through to force kill.
                return process.CloseMainWindow();
            }

            return Kill(process.Id, SigTerm) == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary> Requests force kill and waits until the process exit is confirmed or the wait budget expires. </summary>
    /// <param name="process"> The process that has not yet exited. </param>
    /// <param name="waitTimeout"> The maximum wait time after the force-kill request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> <see cref="ProcessTerminationResult.ForceKilled" /> when exit is confirmed; <see cref="ProcessTerminationResult.ForceKillFailed" /> when it is not. </returns>
    private static async Task<ProcessTerminationResult> ForceKillAndWaitAsync (
        DiagnosticsProcess process,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken)
    {
        var forceKillResult = TryForceKill(process);
        if (forceKillResult != ProcessTerminationResult.ForceKilled)
        {
            return forceKillResult;
        }

        return await WaitUntilExitedAsync(process, waitTimeout, cancellationToken).ConfigureAwait(false)
            ? ProcessTerminationResult.ForceKilled
            : ProcessTerminationResult.ForceKillFailed;
    }

    /// <summary> Requests an immediate process-tree kill and falls back to process-only kill when the tree kill fails. </summary>
    /// <param name="process"> The process that has not yet exited. </param>
    /// <returns> The immediate kill request result before post-kill exit confirmation. </returns>
    private static ProcessTerminationResult TryForceKill (DiagnosticsProcess process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            return ProcessTerminationResult.ForceKilled;
        }
        catch (Exception)
        {
            try
            {
                process.Kill();
                return ProcessTerminationResult.ForceKilled;
            }
            catch (Exception)
            {
                return HasExited(process)
                    ? ProcessTerminationResult.None
                    : ProcessTerminationResult.ForceKillFailed;
            }
        }
    }

    /// <summary> Waits until process exit is observed or the supplied timeout expires. </summary>
    /// <param name="process"> The process to observe. </param>
    /// <param name="timeout"> The maximum wait time. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> <see langword="true" /> when the process exit is observed; otherwise <see langword="false" />. </returns>
    private static async Task<bool> WaitUntilExitedAsync (
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

    /// <summary> Sends one POSIX signal to one process identifier. </summary>
    /// <param name="pid"> The process identifier. </param>
    /// <param name="sig"> The POSIX signal number. </param>
    /// <returns> Zero when the signal request succeeds; otherwise a nonzero value. </returns>
    [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
    private static extern int Kill (
        int pid,
        int sig);
}

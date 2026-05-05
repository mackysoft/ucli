using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Represents daemon shutdown IPC attempt result. </summary>
/// <param name="IsSuccess"> Whether shutdown request succeeded. </param>
/// <param name="IsNotRunning"> Whether daemon endpoint is treated as not running. </param>
/// <param name="Error"> The structured error when shutdown request fails. </param>
internal sealed record DaemonShutdownAttemptResult (
    bool IsSuccess,
    bool IsNotRunning,
    ExecutionError? Error)
{
    /// <summary> Creates a successful shutdown-attempt result. </summary>
    /// <returns> The successful shutdown-attempt result. </returns>
    public static DaemonShutdownAttemptResult Success ()
    {
        return new DaemonShutdownAttemptResult(true, false, null);
    }

    /// <summary> Creates a not-running shutdown-attempt result. </summary>
    /// <returns> The not-running shutdown-attempt result. </returns>
    public static DaemonShutdownAttemptResult NotRunning ()
    {
        return new DaemonShutdownAttemptResult(false, true, null);
    }

    /// <summary> Creates a failed shutdown-attempt result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed shutdown-attempt result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonShutdownAttemptResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonShutdownAttemptResult(false, false, error);
    }
}

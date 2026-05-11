using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;

/// <summary> Represents a daemon launch-attempt read result. </summary>
internal sealed record DaemonLaunchAttemptReadResult (
    DaemonLaunchAttempt? LaunchAttempt,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the read operation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Gets a value indicating whether a launch attempt was found. </summary>
    public bool Exists => LaunchAttempt is not null;

    /// <summary> Creates a successful read result. </summary>
    /// <param name="launchAttempt"> The launch attempt when available. </param>
    /// <returns> The read result. </returns>
    public static DaemonLaunchAttemptReadResult Success (DaemonLaunchAttempt? launchAttempt)
    {
        return new DaemonLaunchAttemptReadResult(launchAttempt, null);
    }

    /// <summary> Creates a failed read result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The read result. </returns>
    public static DaemonLaunchAttemptReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonLaunchAttemptReadResult(null, error);
    }
}

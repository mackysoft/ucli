using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Reachability;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.Reachability;

/// <summary> Implements daemon reachability classification using host-side IPC exception rules. </summary>
internal sealed class DaemonReachabilityClassifier : IDaemonReachabilityClassifier
{
    /// <inheritdoc />
    public bool IsNotRunning (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return DaemonProbeExceptionClassifier.IsNotRunning(exception);
    }

    /// <inheritdoc />
    public bool IsSessionTokenInvalid (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is DaemonPingResponseException pingResponseException
            && pingResponseException.ErrorCode == IpcSessionErrorCodes.SessionTokenInvalid;
    }
}

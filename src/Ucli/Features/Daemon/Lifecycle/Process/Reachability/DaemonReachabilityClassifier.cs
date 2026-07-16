using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Reachability;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

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

    /// <inheritdoc />
    public bool IsRetryableBeforeRequestWrite (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return DaemonIpcConnectionFailureClassifier.IsRetryableBeforeRequestWrite(exception);
    }

    /// <inheritdoc />
    public bool IsRequestTimeout (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is TimeoutException
            || (exception is DaemonPingResponseException pingResponseException
                && pingResponseException.ErrorCode == IpcTransportErrorCodes.IpcTimeout);
    }

    /// <inheritdoc />
    public bool IsRecoverableResponseInterruption (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is IpcResponseReadInterruptedException
            || exception is TimeoutException and not IpcConnectTimeoutException;
    }
}

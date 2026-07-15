using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Recovery;

/// <summary> Classifies connection-phase transport failures that permit a bounded daemon endpoint-availability retry. </summary>
internal static class DaemonIpcConnectionFailureClassifier
{
    /// <summary> Determines whether an expected transport failure occurred before request transmission and may be retried within the endpoint-availability grace period. </summary>
    public static bool IsRetryableBeforeRequestWrite (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is IpcConnectException or IpcConnectTimeoutException;
    }
}

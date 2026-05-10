using System.Net.Sockets;

namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Classifies daemon probe exceptions by IPC transport meaning. </summary>
internal static class DaemonProbeExceptionClassifier
{
    /// <summary> Determines whether one exception means daemon endpoint is not reachable. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when endpoint is treated as not running; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
    public static bool IsNotRunning (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is DaemonPingResponseException pingResponseException)
        {
            return IsSessionAuthenticationRejected(pingResponseException.ErrorCode);
        }

        return exception is SocketException;
    }

    /// <summary> Determines whether one daemon error code indicates session authentication rejection. </summary>
    /// <param name="errorCode"> The daemon error code to classify. </param>
    /// <returns> <see langword="true" /> when the code rejects daemon session authentication; otherwise <see langword="false" />. </returns>
    public static bool IsSessionAuthenticationRejected (UcliErrorCode? errorCode)
    {
        return errorCode == IpcSessionErrorCodes.SessionTokenRequired
            || errorCode == IpcSessionErrorCodes.SessionTokenInvalid;
    }
}

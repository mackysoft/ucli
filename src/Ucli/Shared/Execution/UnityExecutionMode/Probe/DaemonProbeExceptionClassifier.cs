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
            return IsSessionTokenErrorCode(pingResponseException.ErrorCode);
        }

        return exception is SocketException;
    }

    private static bool IsSessionTokenErrorCode (UcliErrorCode? errorCode)
    {
        return errorCode == IpcSessionErrorCodes.SessionTokenRequired
            || errorCode == IpcSessionErrorCodes.SessionTokenInvalid;
    }
}

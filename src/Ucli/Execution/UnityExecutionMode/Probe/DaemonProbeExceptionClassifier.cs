using System.Net.Sockets;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Execution;

/// <summary> Classifies daemon probe exceptions by operational meaning. </summary>
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
            return string.Equals(pingResponseException.ErrorCode, IpcErrorCodes.SessionTokenRequired, StringComparison.Ordinal)
                || string.Equals(pingResponseException.ErrorCode, IpcErrorCodes.SessionTokenInvalid, StringComparison.Ordinal);
        }

        return exception is SocketException;
    }
}
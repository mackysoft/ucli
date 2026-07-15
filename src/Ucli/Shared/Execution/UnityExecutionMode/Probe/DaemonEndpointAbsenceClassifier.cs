using System.Net.Sockets;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Classifies direct IPC endpoint absence evidence for lifecycle decisions that may clean local artifacts. </summary>
internal static class DaemonEndpointAbsenceClassifier
{
    /// <summary> Determines whether one connection-phase failure is direct evidence that an IPC endpoint is absent. </summary>
    /// <param name="exception"> The connection-phase exception to classify. </param>
    /// <returns> <see langword="true" /> when the endpoint is directly known to be absent; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
    public static bool IsDirectEndpointAbsence (IpcConnectException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // NOTE:
        // Unix-domain-socket path loss and other transport errors can coexist with a live listener.
        // Destructive cleanup therefore accepts only ConnectionRefused as direct endpoint absence evidence.
        return exception.InnerException is SocketException socketException
            && socketException.SocketErrorCode == SocketError.ConnectionRefused;
    }
}

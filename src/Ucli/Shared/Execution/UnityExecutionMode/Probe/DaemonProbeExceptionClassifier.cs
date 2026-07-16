using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Classifies daemon probe exceptions by IPC transport meaning. </summary>
internal static class DaemonProbeExceptionClassifier
{
    /// <summary> Determines whether one probe exception permits treating the daemon as not running before a response is received. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> for missing local session metadata or direct endpoint absence; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
    public static bool IsNotRunning (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is DaemonSessionNotAvailableException
            || (exception is IpcConnectException connectException
            && DaemonEndpointAbsenceClassifier.IsDirectEndpointAbsence(connectException));
    }

    /// <summary> Determines whether one daemon error code indicates session authentication rejection. </summary>
    /// <param name="errorCode"> The daemon error code to classify. </param>
    /// <returns> <see langword="true" /> when the code rejects daemon session authentication; otherwise <see langword="false" />. </returns>
    public static bool IsSessionAuthenticationRejected (UcliCode? errorCode)
    {
        return errorCode == IpcSessionErrorCodes.SessionTokenRequired
            || errorCode == IpcSessionErrorCodes.SessionTokenInvalid;
    }
}

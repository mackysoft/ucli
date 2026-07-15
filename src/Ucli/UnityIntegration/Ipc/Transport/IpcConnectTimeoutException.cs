namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Represents timeout while establishing an IPC transport connection before any request bytes are written. </summary>
internal sealed class IpcConnectTimeoutException : TimeoutException
{
    /// <summary> Initializes a new instance of the <see cref="IpcConnectTimeoutException" /> class. </summary>
    /// <param name="message"> The timeout error message. </param>
    /// <param name="innerException"> The inner timeout-related exception. </param>
    public IpcConnectTimeoutException (
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

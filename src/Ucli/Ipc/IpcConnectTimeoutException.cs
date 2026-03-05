namespace MackySoft.Ucli.Ipc;

/// <summary> Represents timeout while establishing IPC transport connection. </summary>
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
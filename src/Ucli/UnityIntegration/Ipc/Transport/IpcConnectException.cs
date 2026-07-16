namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Represents a non-timeout failure while establishing an IPC transport connection before any request bytes are written. </summary>
internal sealed class IpcConnectException : IOException
{
    /// <summary> Initializes an IPC connection failure that occurred before request transmission. </summary>
    /// <param name="message"> The connection failure message. </param>
    /// <param name="innerException"> The transport-specific connection failure. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one argument is <see langword="null" />. </exception>
    public IpcConnectException (
        string message,
        Exception innerException)
        : base(
            message ?? throw new ArgumentNullException(nameof(message)),
            innerException ?? throw new ArgumentNullException(nameof(innerException)))
    {
    }
}

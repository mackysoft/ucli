namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Represents an interrupted IPC response-frame read after request transmission completed. </summary>
internal sealed class IpcResponseReadInterruptedException : IOException
{
    /// <summary> Initializes a new instance of the <see cref="IpcResponseReadInterruptedException" /> class. </summary>
    /// <param name="innerException"> The I/O exception observed while reading the response frame. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="innerException" /> is <see langword="null" />. </exception>
    public IpcResponseReadInterruptedException (IOException innerException)
        : base(innerException?.Message, innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);
    }
}

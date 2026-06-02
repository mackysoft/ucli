namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Classifies connection-local IPC write failures. </summary>
internal static class IpcConnectionWriteFailureClassifier
{
    /// <summary> Determines whether one exception indicates a write failure local to the current IPC connection. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when the exception represents a connection-local write failure; otherwise <see langword="false" />. </returns>
    public static bool IsConnectionLocalWriteFailure (Exception exception)
    {
        return exception is IOException or ObjectDisposedException or InvalidOperationException;
    }
}

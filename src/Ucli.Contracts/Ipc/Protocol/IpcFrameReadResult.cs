namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one IPC frame read result. </summary>
/// <typeparam name="T"> The target model type. </typeparam>
/// <param name="IsSuccess"> Whether frame read and model deserialization succeeded. </param>
/// <param name="Value"> The deserialized model value on success; otherwise default value. </param>
/// <param name="ErrorKind"> The machine-readable error kind on failure. </param>
/// <param name="ErrorMessage"> The diagnostic error message on failure. </param>
public readonly record struct IpcFrameReadResult<T> (
    bool IsSuccess,
    T Value,
    IpcFrameReadErrorKind ErrorKind,
    string ErrorMessage)
{
    /// <summary> Creates one successful frame read result. </summary>
    /// <param name="value"> The deserialized model value. </param>
    /// <returns> The successful result value. </returns>
    public static IpcFrameReadResult<T> Success (T value)
    {
        return new IpcFrameReadResult<T>(
            IsSuccess: true,
            Value: value,
            ErrorKind: IpcFrameReadErrorKind.None,
            ErrorMessage: string.Empty);
    }

    /// <summary> Creates one failed frame read result. </summary>
    /// <param name="errorKind"> The machine-readable error kind. </param>
    /// <param name="errorMessage"> The diagnostic error message. </param>
    /// <returns> The failed result value. </returns>
    public static IpcFrameReadResult<T> Failure (
        IpcFrameReadErrorKind errorKind,
        string errorMessage)
    {
        return new IpcFrameReadResult<T>(
            IsSuccess: false,
            Value: default!,
            ErrorKind: errorKind,
            ErrorMessage: errorMessage);
    }
}

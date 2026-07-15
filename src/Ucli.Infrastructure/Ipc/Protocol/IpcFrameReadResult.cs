namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Represents one IPC frame read result. </summary>
/// <typeparam name="T"> The target model type. </typeparam>
public sealed class IpcFrameReadResult<T>
{
    private IpcFrameReadResult (
        bool isSuccess,
        T value,
        IpcFrameReadErrorKind errorKind,
        string errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    /// <summary> Gets whether frame read and model deserialization succeeded. </summary>
    public bool IsSuccess { get; }

    /// <summary> Gets the deserialized model value on success; otherwise the default value. </summary>
    public T Value { get; }

    /// <summary> Gets the machine-readable error kind on failure. </summary>
    public IpcFrameReadErrorKind ErrorKind { get; }

    /// <summary> Gets the diagnostic error message on failure. </summary>
    public string ErrorMessage { get; }

    /// <summary> Creates one successful frame read result. </summary>
    /// <param name="value"> The deserialized model value. </param>
    /// <returns> The successful result value. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    public static IpcFrameReadResult<T> Success (T value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return new IpcFrameReadResult<T>(
            isSuccess: true,
            value,
            IpcFrameReadErrorKind.None,
            string.Empty);
    }

    /// <summary> Creates one failed frame read result. </summary>
    /// <param name="errorKind"> The machine-readable error kind. </param>
    /// <param name="errorMessage"> The diagnostic error message. </param>
    /// <returns> The failed result value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="errorKind" /> is not a defined failure kind. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="errorMessage" /> is <see langword="null" />. </exception>
    public static IpcFrameReadResult<T> Failure (
        IpcFrameReadErrorKind errorKind,
        string errorMessage)
    {
        if (errorKind == IpcFrameReadErrorKind.None
            || !Enum.IsDefined(typeof(IpcFrameReadErrorKind), errorKind))
        {
            throw new ArgumentOutOfRangeException(nameof(errorKind), errorKind, "Frame read failure kind must be defined and non-None.");
        }

        if (errorMessage == null)
        {
            throw new ArgumentNullException(nameof(errorMessage));
        }

        return new IpcFrameReadResult<T>(
            isSuccess: false,
            value: default!,
            errorKind,
            errorMessage);
    }
}

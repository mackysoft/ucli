namespace MackySoft.Ucli.Foundation;

/// <summary> Represents a structured error returned from foundation services. </summary>
/// <param name="Kind"> The classification used to map the error to the CLI contract. </param>
/// <param name="Message"> The user-facing error message. </param>
internal sealed record ExecutionError (
    ExecutionErrorKind Kind,
    string Message)
{
    /// <summary> Creates an invalid-argument execution error. </summary>
    /// <param name="message"> The user-facing error message. </param>
    /// <returns> The invalid-argument execution error. </returns>
    public static ExecutionError InvalidArgument (string message)
    {
        return new ExecutionError(ExecutionErrorKind.InvalidArgument, message);
    }

    /// <summary> Creates an internal execution error. </summary>
    /// <param name="message"> The user-facing error message. </param>
    /// <returns> The internal execution error. </returns>
    public static ExecutionError InternalError (string message)
    {
        return new ExecutionError(ExecutionErrorKind.InternalError, message);
    }
}

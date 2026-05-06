namespace MackySoft.Ucli.Application.Shared.Foundation;

/// <summary> Represents a structured error returned from foundation services. </summary>
/// <param name="Kind"> The classification used to map the error to the CLI contract. </param>
/// <param name="Message"> The user-facing error message. </param>
/// <param name="Code"> The optional machine-readable error code used when the high-level kind is not specific enough. </param>
internal sealed record ExecutionError (
    ExecutionErrorKind Kind,
    string Message,
    string? Code = null)
{
    /// <summary> Creates an invalid-argument execution error. </summary>
    /// <param name="message"> The user-facing error message. </param>
    /// <param name="code"> The optional machine-readable error code. </param>
    /// <returns> The invalid-argument execution error. </returns>
    public static ExecutionError InvalidArgument (
        string message,
        string? code = null)
    {
        return new ExecutionError(ExecutionErrorKind.InvalidArgument, message, code);
    }

    /// <summary> Creates a timeout execution error. </summary>
    /// <param name="message"> The user-facing error message. </param>
    /// <param name="code"> The optional machine-readable error code. </param>
    /// <returns> The timeout execution error. </returns>
    public static ExecutionError Timeout (
        string message,
        string? code = null)
    {
        return new ExecutionError(ExecutionErrorKind.Timeout, message, code);
    }

    /// <summary> Creates an internal execution error. </summary>
    /// <param name="message"> The user-facing error message. </param>
    /// <param name="code"> The optional machine-readable error code. </param>
    /// <returns> The internal execution error. </returns>
    public static ExecutionError InternalError (
        string message,
        string? code = null)
    {
        return new ExecutionError(ExecutionErrorKind.InternalError, message, code);
    }
}

namespace MackySoft.Ucli.Ops;

/// <summary> Represents one normalized <c>ops</c> service result. </summary>
/// <typeparam name="T"> The successful output type. </typeparam>
/// <param name="Output"> The successful output; otherwise <see langword="null" />. </param>
/// <param name="Message"> The user-facing result message. </param>
/// <param name="ErrorCode"> The machine-readable error code on failure; otherwise <see langword="null" />. </param>
internal sealed record OpsServiceResult<T> (
    T? Output,
    string Message,
    string? ErrorCode)
    where T : class
{
    /// <summary> Gets a value indicating whether the service execution succeeded. </summary>
    public bool IsSuccess => Output is not null && ErrorCode is null;

    /// <summary> Creates a successful service result. </summary>
    /// <param name="output"> The successful output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static OpsServiceResult<T> Success (
        T output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new OpsServiceResult<T>(output, message, null);
    }

    /// <summary> Creates a failed service result. </summary>
    /// <param name="message"> The failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The failed result. </returns>
    public static OpsServiceResult<T> Failure (
        string message,
        string errorCode)
    {
        return new OpsServiceResult<T>(null, message, errorCode);
    }
}
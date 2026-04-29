namespace MackySoft.Ucli.UnityIntegration.Indexing.Core;

/// <summary> Represents one index access result that contains either a value or a machine-readable error. </summary>
/// <typeparam name="T"> The index payload type. </typeparam>
/// <param name="Value"> The payload value on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record IndexAccessResult<T> (
    T? Value,
    IndexServiceError? Error)
    where T : class
{
    /// <summary> Gets a value indicating whether the access operation succeeded. </summary>
    public bool IsSuccess => Value is not null && Error is null;

    /// <summary> Creates a successful index access result. </summary>
    /// <param name="value"> The payload value. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    public static IndexAccessResult<T> Success (T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new IndexAccessResult<T>(value, null);
    }

    /// <summary> Creates a failed index access result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static IndexAccessResult<T> Failure (IndexServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new IndexAccessResult<T>(null, error);
    }

    /// <summary> Creates a failed index access result from error code and message. </summary>
    /// <param name="code"> The error code. </param>
    /// <param name="message"> The error message. </param>
    /// <returns> The failed result. </returns>
    public static IndexAccessResult<T> Failure (
        string code,
        string message)
    {
        return Failure(new IndexServiceError(code, message));
    }
}

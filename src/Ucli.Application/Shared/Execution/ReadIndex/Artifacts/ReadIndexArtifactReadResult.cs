namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one read-index artifact read result. </summary>
/// <typeparam name="T"> The artifact contract type. </typeparam>
/// <param name="Value"> The artifact contract on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured read-index error on failure; otherwise <see langword="null" />. </param>
internal sealed record ReadIndexArtifactReadResult<T> (
    T? Value,
    IndexServiceError? Error)
    where T : class
{
    /// <summary> Gets a value indicating whether the artifact read succeeded. </summary>
    public bool IsSuccess => Value is not null && Error is null;

    /// <summary> Creates a successful artifact read result. </summary>
    /// <param name="value"> The artifact contract. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    public static ReadIndexArtifactReadResult<T> Success (T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ReadIndexArtifactReadResult<T>(value, null);
    }

    /// <summary> Creates a failed artifact read result. </summary>
    /// <param name="error"> The structured read-index error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static ReadIndexArtifactReadResult<T> Failure (IndexServiceError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ReadIndexArtifactReadResult<T>(null, error);
    }

    /// <summary> Creates a failed artifact read result from an error code and message. </summary>
    /// <param name="code"> The machine-readable error code. </param>
    /// <param name="message"> The user-facing error message. </param>
    /// <returns> The failed result. </returns>
    public static ReadIndexArtifactReadResult<T> Failure (
        string code,
        string message)
    {
        return Failure(new IndexServiceError(code, message));
    }
}

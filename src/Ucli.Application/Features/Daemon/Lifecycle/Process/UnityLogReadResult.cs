using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Represents the result of reading Unity log text from local storage. </summary>
/// <param name="Text"> The Unity log text segment when read succeeds; otherwise an empty string. </param>
/// <param name="Truncated"> Whether log text is truncated due to max-bytes limit. </param>
/// <param name="Path"> The Unity log file path. </param>
/// <param name="SizeBytes"> The full log file size in bytes when available. </param>
/// <param name="Error"> The structured error when log read fails; otherwise <see langword="null" />. </param>
internal sealed record UnityLogReadResult (
    string Text,
    bool Truncated,
    string Path,
    long SizeBytes,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether log read operation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful Unity log read result. </summary>
    /// <param name="text"> The Unity log text segment. </param>
    /// <param name="truncated"> Whether log text has been truncated. </param>
    /// <param name="path"> The Unity log file path. </param>
    /// <param name="sizeBytes"> The full Unity log file size in bytes. </param>
    /// <returns> The successful Unity log read result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="text" /> or <paramref name="path" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="sizeBytes" /> is negative. </exception>
    public static UnityLogReadResult Success (
        string text,
        bool truncated,
        string path,
        long sizeBytes)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeBytes);
        return new UnityLogReadResult(text, truncated, path, sizeBytes, null);
    }

    /// <summary> Creates a failed Unity log read result. </summary>
    /// <param name="path"> The Unity log file path. </param>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed Unity log read result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="path" /> or <paramref name="error" /> is <see langword="null" />. </exception>
    public static UnityLogReadResult Failure (
        string path,
        ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(error);
        return new UnityLogReadResult(string.Empty, false, path, 0, error);
    }
}

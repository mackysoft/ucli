using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Represents the result of reading daemon log text from local storage. </summary>
/// <param name="Text"> The daemon log text segment when read succeeds; otherwise an empty string. </param>
/// <param name="Truncated"> Whether log text is truncated due to max-bytes limit. </param>
/// <param name="Path"> The daemon log file path. </param>
/// <param name="SizeBytes"> The full log file size in bytes when available. </param>
/// <param name="Error"> The structured error when log read fails; otherwise <see langword="null" />. </param>
internal sealed record DaemonLogReadResult (
    string Text,
    bool Truncated,
    string Path,
    long SizeBytes,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether log read operation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful daemon log read result. </summary>
    /// <param name="text"> The daemon log text segment. </param>
    /// <param name="truncated"> Whether log text has been truncated. </param>
    /// <param name="path"> The daemon log file path. </param>
    /// <param name="sizeBytes"> The full daemon log file size in bytes. </param>
    /// <returns> The successful daemon log read result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="text" /> or <paramref name="path" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="sizeBytes" /> is negative. </exception>
    public static DaemonLogReadResult Success (
        string text,
        bool truncated,
        string path,
        long sizeBytes)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeBytes);
        return new DaemonLogReadResult(text, truncated, path, sizeBytes, null);
    }

    /// <summary> Creates a failed daemon log read result. </summary>
    /// <param name="path"> The daemon log file path. </param>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed daemon log read result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="path" /> or <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonLogReadResult Failure (
        string path,
        ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonLogReadResult(string.Empty, false, path, 0, error);
    }
}
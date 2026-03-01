using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Represents the result of reading daemon session metadata from local storage. </summary>
/// <param name="Session"> The loaded daemon session when one exists and read succeeds; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error when read fails; otherwise <see langword="null" />. </param>
internal sealed record DaemonSessionReadResult (
    DaemonSession? Session,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether session read operation succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Gets a value indicating whether a daemon session exists. </summary>
    public bool Exists => Session is not null;

    /// <summary> Creates a successful read result. </summary>
    /// <param name="session"> The loaded daemon session when one exists; otherwise <see langword="null" />. </param>
    /// <returns> The successful read result. </returns>
    public static DaemonSessionReadResult Success (DaemonSession? session)
    {
        return new DaemonSessionReadResult(session, null);
    }

    /// <summary> Creates a failed read result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed read result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonSessionReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonSessionReadResult(null, error);
    }
}
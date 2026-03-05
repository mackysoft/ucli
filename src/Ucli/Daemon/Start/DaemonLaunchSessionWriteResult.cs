using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon.Start;

/// <summary> Represents a daemon launch-session persistence result. </summary>
/// <param name="Session"> The persisted daemon session on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured error on failure; otherwise <see langword="null" />. </param>
internal sealed record DaemonLaunchSessionWriteResult (
    DaemonSession? Session,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether launch-session persistence succeeded. </summary>
    public bool IsSuccess => Session is not null && Error is null;

    /// <summary> Creates a successful launch-session persistence result. </summary>
    /// <param name="session"> The persisted daemon session. </param>
    /// <returns> The successful launch-session persistence result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static DaemonLaunchSessionWriteResult Success (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonLaunchSessionWriteResult(session, null);
    }

    /// <summary> Creates a failed launch-session persistence result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed launch-session persistence result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonLaunchSessionWriteResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonLaunchSessionWriteResult(null, error);
    }
}
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Represents the result of daemon start operation. </summary>
/// <param name="Status"> The daemon start outcome. </param>
/// <param name="Session"> The daemon session metadata when start succeeds or daemon is already running. </param>
/// <param name="Error"> The structured error when start fails. </param>
internal sealed record DaemonStartResult (
    DaemonStartStatus Status,
    DaemonSession? Session,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon start succeeded or detected an already-running daemon. </summary>
    public bool IsSuccess => (Status == DaemonStartStatus.Started || Status == DaemonStartStatus.AlreadyRunning)
        && Session is not null
        && Error is null;

    /// <summary> Creates a successful start result. </summary>
    /// <param name="session"> The started daemon session metadata. </param>
    /// <returns> The successful start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static DaemonStartResult Started (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonStartResult(DaemonStartStatus.Started, session, null);
    }

    /// <summary> Creates an already-running result. </summary>
    /// <param name="session"> The existing daemon session metadata. </param>
    /// <returns> The already-running result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static DaemonStartResult AlreadyRunning (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonStartResult(DaemonStartStatus.AlreadyRunning, session, null);
    }

    /// <summary> Creates a failed start result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed start result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStartResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStartResult(DaemonStartStatus.Failed, null, error);
    }
}
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Represents the result of daemon status query operation. </summary>
/// <param name="Status"> The daemon status outcome. </param>
/// <param name="Session"> The daemon session metadata when available. </param>
/// <param name="Diagnosis"> The daemon diagnosis metadata when available. </param>
/// <param name="Error"> The structured error when status query fails. </param>
internal sealed record DaemonStatusResult (
    DaemonStatusKind Status,
    DaemonSession? Session,
    DaemonDiagnosis? Diagnosis,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether daemon status query succeeded. </summary>
    public bool IsSuccess => Status != DaemonStatusKind.Failed && Error is null;

    /// <summary> Creates a running result. </summary>
    /// <param name="session"> The current daemon session metadata. </param>
    /// <returns> The running result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static DaemonStatusResult Running (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonStatusResult(DaemonStatusKind.Running, session, null, null);
    }

    /// <summary> Creates a not-running result. </summary>
    /// <param name="diagnosis"> The persisted daemon diagnosis metadata when available. </param>
    /// <returns> The not-running result. </returns>
    public static DaemonStatusResult NotRunning (DaemonDiagnosis? diagnosis = null)
    {
        return new DaemonStatusResult(DaemonStatusKind.NotRunning, null, diagnosis, null);
    }

    /// <summary> Creates a stale-session result. </summary>
    /// <param name="session"> The stale daemon session metadata. </param>
    /// <param name="diagnosis"> The daemon diagnosis metadata when available. </param>
    /// <returns> The stale-session result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public static DaemonStatusResult Stale (
        DaemonSession session,
        DaemonDiagnosis? diagnosis = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonStatusResult(DaemonStatusKind.Stale, session, diagnosis, null);
    }

    /// <summary> Creates a failed status-query result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed status-query result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static DaemonStatusResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStatusResult(DaemonStatusKind.Failed, null, null, error);
    }
}

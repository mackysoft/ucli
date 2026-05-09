using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Represents the bounded observation result for one CLI-launched GUI daemon startup attempt. </summary>
/// <param name="Session"> The registered GUI daemon session when startup succeeds. </param>
/// <param name="Blocker"> The terminal startup blocker when one is observed. </param>
/// <param name="Error"> The structured error when observation fails or times out. </param>
internal sealed record DaemonGuiStartupObservationResult (
    DaemonSession? Session,
    DaemonGuiStartupBlocker? Blocker,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether GUI daemon startup succeeded. </summary>
    public bool IsSuccess => Session is not null && Blocker is null && Error is null;

    /// <summary> Gets a value indicating whether GUI daemon startup was blocked by a known terminal condition. </summary>
    public bool IsBlocked => Session is null && Blocker is not null && Error is null;

    /// <summary> Creates a successful observation result. </summary>
    public static DaemonGuiStartupObservationResult Success (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonGuiStartupObservationResult(session, null, null);
    }

    /// <summary> Creates a blocked observation result. </summary>
    public static DaemonGuiStartupObservationResult Blocked (DaemonGuiStartupBlocker blocker)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        return new DaemonGuiStartupObservationResult(null, blocker, null);
    }

    /// <summary> Creates a failed observation result. </summary>
    public static DaemonGuiStartupObservationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonGuiStartupObservationResult(null, null, error);
    }
}

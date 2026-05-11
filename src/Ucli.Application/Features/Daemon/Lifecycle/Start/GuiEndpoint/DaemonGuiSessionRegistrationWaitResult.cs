using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiEndpoint;

/// <summary> Represents the result of waiting for a GUI daemon session registration. </summary>
/// <param name="Session"> The matching GUI daemon session when registration completed. </param>
/// <param name="LifecycleSnapshot"> The endpoint-registered lifecycle snapshot when registration completed. </param>
/// <param name="Error"> The structured wait error when registration did not complete. </param>
internal sealed record DaemonGuiSessionRegistrationWaitResult (
    DaemonSession? Session,
    DaemonStartLifecycleSnapshot? LifecycleSnapshot,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the wait succeeded. </summary>
    public bool IsSuccess => Session is not null && Error is null;

    /// <summary> Creates a successful GUI session wait result. </summary>
    public static DaemonGuiSessionRegistrationWaitResult Success (
        DaemonSession session,
        DaemonStartLifecycleSnapshot? lifecycleSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new DaemonGuiSessionRegistrationWaitResult(session, lifecycleSnapshot, null);
    }

    /// <summary> Creates a failed GUI session wait result. </summary>
    public static DaemonGuiSessionRegistrationWaitResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonGuiSessionRegistrationWaitResult(null, null, error);
    }
}

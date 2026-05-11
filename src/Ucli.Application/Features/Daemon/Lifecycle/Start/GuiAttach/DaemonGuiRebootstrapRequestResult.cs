using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiAttach;

/// <summary> Represents the outcome of requesting GUI daemon endpoint rebootstrap from an existing Editor process. </summary>
internal sealed record DaemonGuiRebootstrapRequestResult (
    bool IsAccepted,
    ExecutionError? Error)
{
    public static DaemonGuiRebootstrapRequestResult Accepted ()
    {
        return new DaemonGuiRebootstrapRequestResult(true, null);
    }

    public static DaemonGuiRebootstrapRequestResult Unavailable (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonGuiRebootstrapRequestResult(false, error);
    }
}

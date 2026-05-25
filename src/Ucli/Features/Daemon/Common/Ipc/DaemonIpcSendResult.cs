using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Common.Ipc;

/// <summary> Represents one daemon IPC send result after session resolution and recovery handling. </summary>
internal sealed record DaemonIpcSendResult (
    IpcResponse? Response,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the send completed with a daemon response. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates a successful send result. </summary>
    public static DaemonIpcSendResult Success (IpcResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new DaemonIpcSendResult(response, null);
    }

    /// <summary> Creates a failed send result. </summary>
    public static DaemonIpcSendResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonIpcSendResult(null, error);
    }
}

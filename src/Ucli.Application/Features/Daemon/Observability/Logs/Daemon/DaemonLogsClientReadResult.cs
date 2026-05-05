using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;

/// <summary> Represents one daemon-log IPC read attempt result. </summary>
/// <param name="Response"> The decoded response payload when read succeeds. </param>
/// <param name="Error"> The structured error when read fails. </param>
internal sealed record DaemonLogsClientReadResult (
    IpcDaemonLogsReadResponse? Response,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the read attempt succeeded. </summary>
    public bool IsSuccess => Response is not null && Error is null;

    /// <summary> Creates a successful client read result. </summary>
    /// <param name="response"> The decoded response payload. </param>
    /// <returns> The successful client read result. </returns>
    public static DaemonLogsClientReadResult Success (IpcDaemonLogsReadResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new DaemonLogsClientReadResult(response, null);
    }

    /// <summary> Creates a failed client read result. </summary>
    /// <param name="error"> The structured execution error. </param>
    /// <returns> The failed client read result. </returns>
    public static DaemonLogsClientReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonLogsClientReadResult(null, error);
    }
}

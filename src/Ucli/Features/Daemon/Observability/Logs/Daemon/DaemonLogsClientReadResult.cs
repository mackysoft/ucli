using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;

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

using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Encodes daemon-log query values into <c>daemon.logs.read</c> IPC request envelopes. </summary>
internal static class IpcDaemonLogsRequestCodec
{
    /// <summary> Creates one daemon-log read IPC request envelope. </summary>
    /// <param name="query"> The daemon-log query values. </param>
    /// <param name="sessionToken"> The daemon session token used for authorization. </param>
    /// <returns> The encoded IPC request envelope. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="query" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionToken" /> is empty or whitespace. </exception>
    public static IpcRequest CreateRequest (
        IpcDaemonLogsReadRequest query,
        string sessionToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        var payload = IpcPayloadCodec.SerializeToElement(query);
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"daemon-logs-read-{Guid.NewGuid():N}",
            SessionToken: sessionToken,
            Method: IpcMethodNames.DaemonLogsRead,
            Payload: payload);
    }
}

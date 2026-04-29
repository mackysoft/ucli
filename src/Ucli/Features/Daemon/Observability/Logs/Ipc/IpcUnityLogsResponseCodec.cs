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
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Decodes Unity-log IPC response envelopes into validated payload values. </summary>
internal static class IpcUnityLogsResponseCodec
{
    /// <summary> Tries to decode one Unity-log IPC response. </summary>
    /// <param name="response"> The IPC response envelope. </param>
    /// <param name="payload"> The decoded Unity-log payload when decode succeeds. </param>
    /// <param name="error"> The structured decode error when decode fails. </param>
    /// <returns> <see langword="true" /> when decode succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryDecode (
        IpcResponse response,
        out IpcUnityLogsReadResponse? payload,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (IpcResponseFailureReader.TryRead(response, out var firstError, out var status))
        {
            if (firstError is not null)
            {
                error = string.Equals(firstError.Code, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal)
                    ? ExecutionError.InvalidArgument($"Unity logs read failed with error code '{firstError.Code}'. {firstError.Message}")
                    : ExecutionError.InternalError($"Unity logs read failed with error code '{firstError.Code}'. {firstError.Message}");
                payload = null;
                return false;
            }

            error = ExecutionError.InternalError($"Unity logs read failed with status '{status}'.");
            payload = null;
            return false;
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcUnityLogsReadResponse parsedPayload, out var readError))
        {
            error = ExecutionError.InternalError($"Unity logs read payload is invalid. {readError.Message}");
            payload = null;
            return false;
        }

        if (parsedPayload.Events is null)
        {
            error = ExecutionError.InternalError("Unity logs read payload is invalid. Property 'events' must not be null.");
            payload = null;
            return false;
        }

        if (string.IsNullOrWhiteSpace(parsedPayload.NextCursor))
        {
            error = ExecutionError.InternalError("Unity logs read payload is invalid. Property 'nextCursor' must not be empty.");
            payload = null;
            return false;
        }

        foreach (var unityLogEvent in parsedPayload.Events)
        {
            if (unityLogEvent is null
                || string.IsNullOrWhiteSpace(unityLogEvent.Timestamp)
                || string.IsNullOrWhiteSpace(unityLogEvent.Level)
                || string.IsNullOrWhiteSpace(unityLogEvent.Source)
                || string.IsNullOrWhiteSpace(unityLogEvent.Message)
                || string.IsNullOrWhiteSpace(unityLogEvent.Cursor))
            {
                error = ExecutionError.InternalError("Unity logs read payload is invalid. One or more event fields are missing.");
                payload = null;
                return false;
            }
        }

        payload = parsedPayload;
        error = null;
        return true;
    }
}

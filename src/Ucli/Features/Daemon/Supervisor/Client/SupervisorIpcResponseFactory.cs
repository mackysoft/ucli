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
using MackySoft.Ucli.UnityIntegration.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Client;

/// <summary> Creates supervisor IPC responses for successful and failed request handling. </summary>
internal static class SupervisorIpcResponseFactory
{
    /// <summary> Creates one successful supervisor response for the specified request and payload. </summary>
    /// <typeparam name="TPayload">The payload model type.</typeparam>
    /// <param name="request"> The incoming request. </param>
    /// <param name="payload"> The response payload. </param>
    /// <returns> The serialized success response. </returns>
    public static IpcResponse CreateSuccessResponse<TPayload> (
        IpcRequest request,
        TPayload payload)
    {
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: Array.Empty<IpcError>());
    }

    /// <summary> Creates one failed supervisor response for the specified request. </summary>
    /// <param name="request"> The incoming request. </param>
    /// <param name="code"> The IPC error code. </param>
    /// <param name="message"> The IPC error message. </param>
    /// <returns> The serialized error response. </returns>
    public static IpcResponse CreateErrorResponse (
        IpcRequest request,
        string code,
        string message)
    {
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(new { }),
            Errors:
            [
                new IpcError(code, message, null),
            ]);
    }

    /// <summary> Creates one failed response for a malformed incoming IPC frame. </summary>
    /// <param name="errorKind"> The frame read-error kind. </param>
    /// <param name="errorMessage"> The frame read-error message. </param>
    /// <returns> The serialized malformed-frame response. </returns>
    public static IpcResponse CreateMalformedFrameResponse (
        IpcFrameReadErrorKind errorKind,
        string errorMessage)
    {
        var code = errorKind == IpcFrameReadErrorKind.PayloadTooLarge
            ? IpcErrorCodes.IpcFrameTooLarge
            : IpcErrorCodes.InvalidArgument;
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: string.Empty,
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(new { }),
            Errors:
            [
                new IpcError(code, $"Supervisor IPC request frame is invalid. {errorMessage}", null),
            ]);
    }
}
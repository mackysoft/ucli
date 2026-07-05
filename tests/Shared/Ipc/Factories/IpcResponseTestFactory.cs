using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class IpcResponseTestFactory
{
    public static IpcResponse CreateSuccess<TPayload> (
        IpcRequest request,
        TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: Array.Empty<IpcError>());
    }

    public static IpcResponse CreateError (
        IpcRequest request,
        UcliCode code,
        string message,
        string? opId = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(new { }),
            Errors:
            [
                new IpcError(code, message, opId),
            ]);
    }
}

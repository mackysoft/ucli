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
            protocolVersion: request.ProtocolVersion,
            requestId: request.RequestId,
            status: IpcProtocol.StatusOk,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors: Array.Empty<IpcError>());
    }

    public static IpcResponse CreateError (
        IpcRequest request,
        UcliCode code,
        string message,
        string? opId = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new IpcResponse(
            protocolVersion: request.ProtocolVersion,
            requestId: request.RequestId,
            status: IpcProtocol.StatusError,
            payload: IpcPayloadCodec.SerializeToElement(new { }),
            errors:
            [
                new IpcError(code, message, opId),
            ]);
    }
}

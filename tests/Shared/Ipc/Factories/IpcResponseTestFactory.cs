using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class IpcResponseTestFactory
{
    public static IpcResponse CreateSuccess<TPayload> (
        IpcRequestEnvelope request,
        TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new IpcResponse(
            protocolVersion: request.ProtocolVersion,
            requestId: request.RequestId,
            status: IpcResponseStatus.Ok,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors: Array.Empty<IpcError>());
    }

    public static IpcResponse CreateError (
        IpcRequestEnvelope request,
        UcliCode code,
        string message,
        IpcExecuteStepId? opId = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new IpcResponse(
            protocolVersion: request.ProtocolVersion,
            requestId: request.RequestId,
            status: IpcResponseStatus.Error,
            payload: IpcPayloadCodec.SerializeToElement(new { }),
            errors:
            [
                new IpcError(code, message, opId),
            ]);
    }
}

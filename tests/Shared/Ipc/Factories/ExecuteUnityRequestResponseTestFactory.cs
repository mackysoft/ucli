using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class ExecuteUnityRequestResponseTestFactory
{
    private const string DefaultRequestId = "req-1";

    public static UnityRequestResponse Create (
        string status,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        string? planToken = null,
        OperationExecutionReadPostcondition? readPostcondition = null,
        IpcProjectIdentity? project = null,
        string requestId = DefaultRequestId)
    {
        var payload = new IpcExecuteResponse(opResults)
        {
            PlanToken = planToken,
            ReadPostcondition = readPostcondition == null
                ? null
                : ReadPostconditionTestFactory.ToIpcContract(readPostcondition),
        };
        if (project != null)
        {
            payload = payload with
            {
                Project = project,
            };
        }

        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: status,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: errors));
    }
}

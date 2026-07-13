using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class ExecuteUnityRequestResponseTestFactory
{
    public static UnityRequestResponse Create (
        string status,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        string? planToken = null,
        OperationExecutionReadPostcondition? readPostcondition = null,
        IpcProjectIdentity? project = null)
    {
        var payload = new IpcExecuteResponse(opResults)
        {
            PlanToken = planToken,
            ReadPostcondition = readPostcondition == null
                ? null
                : ReadPostconditionTestFactory.ToIpcContract(readPostcondition),
        };
        payload = payload with
        {
            Project = project ?? new IpcProjectIdentity(
                ProjectPath: "/repo/UnityProject",
                ProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint").ToString(),
                UnityVersion: "6000.1.4f1"),
        };

        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: status,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors: errors));
    }
}

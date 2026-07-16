using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class ExecuteUnityRequestResponseTestFactory
{
    public static UnityRequestResponse Create (
        IpcResponseStatus status,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        string? planToken = null,
        IpcExecuteReadPostcondition? readPostcondition = null,
        IpcProjectIdentity? project = null)
    {
        var payload = new IpcExecuteResponse(
            opResults,
            project ?? new IpcProjectIdentity(
                projectPath: ProjectPathTestValues.RepositoryUnityProject,
                projectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
                unityVersion: "6000.1.4f1"),
            planToken: planToken,
            readPostcondition: readPostcondition,
            postReadSource: null,
            contractViolations: null);

        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: status,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors: errors));
    }
}

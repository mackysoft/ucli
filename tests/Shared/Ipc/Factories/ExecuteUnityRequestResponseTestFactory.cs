using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.TestSupport;

internal static class ExecuteUnityRequestResponseTestFactory
{
    public static UnityRequestResponse Create (
        IpcResponseStatus status,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        string? planToken = null,
        ExecutionReadPostcondition? readPostcondition = null,
        UnityProjectIdentity? project = null)
    {
        var payload = new IpcExecuteResponse(
            opResults,
            project ?? new UnityProjectIdentity(
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

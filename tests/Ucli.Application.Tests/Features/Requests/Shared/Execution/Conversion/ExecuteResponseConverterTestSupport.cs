using System.Text.Json;

using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

internal static class ExecuteResponseConverterTestSupport
{
    public static ProjectFingerprint ExpectedProjectFingerprint { get; } =
        ProjectFingerprintTestFactory.Create("project-fingerprint");

    public static ResolvedUnityProjectContext ExpectedProject { get; } = ResolvedUnityProjectContext.Create(
        unityProjectRoot: ProjectPathTestValues.RepositoryUnityProject,
        repositoryRoot: ProjectPathTestValues.RepositoryRoot,
        projectFingerprint: ExpectedProjectFingerprint,
        pathSource: UnityProjectPathSource.CommandOption,
        pathSourceLabel: null,
        unityVersion: "6000.1.4f1");

    public static string ExpectedProjectPathJson { get; } = JsonSerializer.Serialize(ExpectedProject.UnityProjectRoot);

    public static UnityRequestResponse CreateResponse (IpcExecuteResponse payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: []);
    }

    public static UnityRequestResponse CreateResponse (string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return new UnityRequestResponse(
            Payload: document.RootElement.Clone(),
            Errors: []);
    }

    public static IpcExecuteResponse CreateExecuteResponse (
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IpcExecutePostReadSource? postReadSource = null,
        IReadOnlyList<IpcExecuteContractViolation>? contractViolations = null)
    {
        ArgumentNullException.ThrowIfNull(opResults);
        return new IpcExecuteResponse(
            opResults,
            CreateProjectIdentity(),
            planToken: null,
            readPostcondition: null,
            postReadSource: postReadSource,
            contractViolations: contractViolations);
    }

    public static IpcProjectIdentity CreateProjectIdentity ()
    {
        return new IpcProjectIdentity(
            projectPath: ExpectedProject.UnityProjectRoot,
            projectFingerprint: ExpectedProjectFingerprint,
            unityVersion: "6000.1.4f1");
    }
}

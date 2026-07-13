using System.Text.Json;

using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

internal static class ExecuteResponseConverterTestSupport
{
    public static ProjectFingerprint ExpectedProjectFingerprint { get; } =
        ProjectFingerprintTestFactory.Create("project-fingerprint");

    public static UnityRequestResponse CreateResponse (IpcExecuteResponse payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: [],
            HasFailureStatus: false);
    }

    public static UnityRequestResponse CreateResponse (string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return new UnityRequestResponse(
            Payload: document.RootElement.Clone(),
            Errors: [],
            HasFailureStatus: false);
    }

    public static IpcExecuteResponse CreateExecuteResponse (
        IReadOnlyList<IpcExecuteOperationResult> opResults)
    {
        ArgumentNullException.ThrowIfNull(opResults);
        return new IpcExecuteResponse(opResults, CreateProjectIdentity());
    }

    public static IpcProjectIdentity CreateProjectIdentity ()
    {
        return new IpcProjectIdentity(
            projectPath: "/repo/UnityProject",
            projectFingerprint: ExpectedProjectFingerprint,
            unityVersion: "6000.1.4f1");
    }
}

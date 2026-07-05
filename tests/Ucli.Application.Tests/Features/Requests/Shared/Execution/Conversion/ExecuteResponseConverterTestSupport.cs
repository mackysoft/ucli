using System.Text.Json;

using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Requests.Shared.Execution.Conversion;

internal static class ExecuteResponseConverterTestSupport
{
    public static UnityRequestResponse CreateResponse (IpcExecuteResponse payload)
    {
        if (payload.Project == IpcProjectIdentity.Unknown)
        {
            payload = payload with
            {
                Project = CreateProjectIdentity(),
            };
        }

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

    public static IpcProjectIdentity CreateProjectIdentity ()
    {
        return new IpcProjectIdentity(
            ProjectPath: "/repo/UnityProject",
            ProjectFingerprint: "project-fingerprint",
            UnityVersion: "6000.1.4f1");
    }
}

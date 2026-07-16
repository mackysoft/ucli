using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Execution.Mode;

internal static class IpcDaemonPingClientTestSupport
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    public static ResolvedUnityProjectContext CreateFingerprintMatchedProject ()
    {
        return ResolvedUnityProjectContextTestFactory.Create(projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"));
    }

    public static RecordingDaemonSessionStore CreateResolvedSessionStore (string sessionToken)
    {
        return new RecordingDaemonSessionStore(
            DaemonSessionReadResultTestFactory.FoundForToken(sessionToken));
    }

    public static UnexpectedDaemonSessionStore CreateUnexpectedSessionStore (
        string reason)
    {
        return new UnexpectedDaemonSessionStore(reason);
    }

    public static RecordingIpcTransportClient CreateSuccessfulPingTransportClient ()
    {
        return new RecordingIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcResponseStatus.Ok,
                Array.Empty<IpcError>(),
                IpcUnityEditorObservationTestFactory.Create(
                    projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"))));
    }

    public static IpcResponse CreateResponse (
        IpcRequestEnvelope request,
        IpcResponseStatus status,
        IReadOnlyList<IpcError> errors,
        object? payload = null)
    {
        return new IpcResponse(
            protocolVersion: request.ProtocolVersion,
            requestId: request.RequestId,
            status: status,
            payload: payload is null
                ? JsonDocument.Parse("{}").RootElement.Clone()
                : IpcPayloadCodec.SerializeToElement(payload),
            errors: errors);
    }

}

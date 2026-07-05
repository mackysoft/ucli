using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Execution.Mode;

internal static class IpcDaemonPingClientTestSupport
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    public static ResolvedUnityProjectContext CreateFingerprintMatchedProject ()
    {
        return ResolvedUnityProjectContextTestFactory.Create(projectFingerprint: "fingerprint");
    }

    public static StaticDaemonSessionConnectionProvider CreateResolvedSessionProvider (string sessionToken = "resolved-token")
    {
        return new StaticDaemonSessionConnectionProvider(CreateConnectionResult(sessionToken));
    }

    public static RecordingIpcTransportClient CreateSuccessfulPingTransportClient ()
    {
        return new RecordingIpcTransportClient(request =>
            CreateResponse(
                request,
                IpcProtocol.StatusOk,
                Array.Empty<IpcError>(),
                IpcPingResponseTestFactory.Create(projectFingerprint: "fingerprint")));
    }

    public static IpcResponse CreateResponse (
        IpcRequest request,
        string status,
        IReadOnlyList<IpcError> errors,
        object? payload = null)
    {
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: status,
            Payload: payload is null
                ? JsonDocument.Parse("{}").RootElement.Clone()
                : IpcPayloadCodec.SerializeToElement(payload),
            Errors: errors);
    }

    private static DaemonSessionConnectionResolutionResult CreateConnectionResult (string sessionToken)
    {
        return DaemonSessionConnectionResolutionResult.Success(new DaemonSessionConnection(
            sessionToken,
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-session.sock")));
    }
}

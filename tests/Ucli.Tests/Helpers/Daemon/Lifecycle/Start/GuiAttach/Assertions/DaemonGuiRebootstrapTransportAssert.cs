using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonGuiRebootstrapTransportAssert
{
    public static void NoIpcRequestWasSent (StubIpcTransportClient transportClient)
    {
        Assert.Empty(transportClient.Invocations);
    }

    public static StubIpcTransportInvocation RebootstrapRequestedForManifest (
        StubIpcTransportClient transportClient,
        GuiSupervisorManifestJsonContract expectedManifest,
        ProjectFingerprint expectedProjectFingerprint,
        TimeSpan expectedTimeout)
    {
        var invocation = Assert.Single(transportClient.Invocations);
        Assert.False(invocation.UsesUnboundedResponseWait);
        Assert.Equal(ContractLiteralCodec.ToValue(UnityIpcMethod.GuiRebootstrap), invocation.Request.Method);
        Assert.Equal(expectedManifest.SessionToken.GetEncodedValue(), invocation.Request.SessionToken);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Single), invocation.Request.ResponseMode);
        Assert.Equal(expectedManifest.Endpoint, invocation.Endpoint);
        Assert.InRange(invocation.Timeout, TimeSpan.FromTicks(1), expectedTimeout);

        Assert.True(
            IpcPayloadCodec.TryDeserialize(invocation.Request.Payload, out IpcGuiRebootstrapRequest payload, out var payloadError),
            payloadError.Message);
        Assert.Equal(expectedProjectFingerprint, payload.ProjectFingerprint);
        Assert.True(payload.ReplaceExistingSession);
        return invocation;
    }
}

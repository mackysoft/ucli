using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcCommonContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcTransportKind_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)IpcTransportKind.NamedPipe);
        Assert.Equal(1, (int)IpcTransportKind.UnixDomainSocket);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcEndpoint_ConstructedValuesAreRetained ()
    {
        var endpoint = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli.sock");

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint.TransportKind);
        Assert.Equal("/tmp/ucli.sock", endpoint.Address);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonBootstrapArgumentNames_HasStableStringValues ()
    {
        Assert.Equal("-ucliRepositoryRoot", IpcDaemonBootstrapArgumentNames.RepositoryRoot);
        Assert.Equal("-ucliProjectFingerprint", IpcDaemonBootstrapArgumentNames.ProjectFingerprint);
        Assert.Equal("-ucliSessionPath", IpcDaemonBootstrapArgumentNames.SessionPath);
        Assert.Equal("-ucliEndpointTransportKind", IpcDaemonBootstrapArgumentNames.EndpointTransportKind);
        Assert.Equal("-ucliEndpointAddress", IpcDaemonBootstrapArgumentNames.EndpointAddress);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonBootstrapArgumentsCodec_HasStableUnityExecuteMethodName ()
    {
        Assert.Equal(
            "MackySoft.Ucli.Unity.Ipc.UnityDaemonBootstrap.Start",
            IpcDaemonBootstrapArgumentsCodec.UnityExecuteMethodName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcJsonSerializerOptions_Default_HasStableConfiguration ()
    {
        var options = IpcJsonSerializerOptions.Default;

        Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.True(options.PropertyNameCaseInsensitive);
        Assert.False(options.WriteIndented);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTransportKindCodec_ToValue_ReturnsStableLiterals ()
    {
        Assert.Equal(IpcTransportKindValues.NamedPipe, IpcTransportKindCodec.ToValue(IpcTransportKind.NamedPipe));
        Assert.Equal(IpcTransportKindValues.UnixDomainSocket, IpcTransportKindCodec.ToValue(IpcTransportKind.UnixDomainSocket));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTransportKindCodec_TryParse_AcceptsKnownValues ()
    {
        Assert.True(IpcTransportKindCodec.TryParse(IpcTransportKindValues.NamedPipe, out var namedPipe));
        Assert.Equal(IpcTransportKind.NamedPipe, namedPipe);
        Assert.True(IpcTransportKindCodec.TryParse(IpcTransportKindValues.UnixDomainSocket, out var unixDomainSocket));
        Assert.Equal(IpcTransportKind.UnixDomainSocket, unixDomainSocket);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcTransportKindCodec_TryParse_UnknownValue_ReturnsFalse ()
    {
        Assert.False(IpcTransportKindCodec.TryParse("unsupported", out _));
    }
}
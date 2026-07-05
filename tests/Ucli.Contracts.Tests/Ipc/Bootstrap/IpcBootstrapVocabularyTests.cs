using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Bootstrap;

public sealed class IpcBootstrapVocabularyTests
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
    public void IpcBatchmodeBootstrapArgumentNames_HasStableStringValues ()
    {
        Assert.Equal("-ucliBootstrapTarget", IpcBatchmodeBootstrapArgumentNames.Target);
        Assert.Equal("-ucliProjectFingerprint", IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBatchmodeBootstrapTargetValues_HasStableStringValues ()
    {
        Assert.Equal("daemon", IpcBatchmodeBootstrapTargetValues.Daemon);
        Assert.Equal("oneshot", IpcBatchmodeBootstrapTargetValues.Oneshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonBootstrapArgumentNames_HasStableStringValues ()
    {
        Assert.Equal("-ucliRepositoryRoot", IpcDaemonBootstrapArgumentNames.RepositoryRoot);
        Assert.Equal("-ucliSessionPath", IpcDaemonBootstrapArgumentNames.SessionPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcEndpointBootstrapArgumentNames_HasStableStringValues ()
    {
        Assert.Equal("-ucliEndpointTransportKind", IpcEndpointBootstrapArgumentNames.TransportKind);
        Assert.Equal("-ucliEndpointAddress", IpcEndpointBootstrapArgumentNames.Address);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcOneshotBootstrapArgumentNames_HasStableStringValues ()
    {
        Assert.Equal("-ucliOneshotParentProcessId", IpcOneshotBootstrapArgumentNames.ParentProcessId);
        Assert.Equal("-ucliOneshotSessionToken", IpcOneshotBootstrapArgumentNames.SessionToken);
        Assert.Equal("-ucliOneshotExitDeadlineUtc", IpcOneshotBootstrapArgumentNames.ExitDeadlineUtc);
    }
}

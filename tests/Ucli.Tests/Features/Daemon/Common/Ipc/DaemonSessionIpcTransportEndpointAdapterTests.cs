using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonSessionIpcTransportEndpointAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WithUnixSocketContract_ValidatesOnceAndBindsNormalizedRuntimeEndpoint ()
    {
        var sourceSession = DaemonSessionTestFactory.Create();
        var rawSocketPath = Path.Combine(
            Path.GetTempPath(),
            ".",
            "ucli-session-boundary.sock");
        var contract = DaemonSessionContractMapper.ToContract(sourceSession) with
        {
            EndpointTransportKind = IpcTransportKind.UnixDomainSocket,
            EndpointAddress = rawSocketPath,
        };

        var isValid = DaemonSessionIpcTransportEndpointAdapter.TryCreate(
            contract,
            sourceSession.ProjectFingerprint,
            "test session contract.",
            out var session,
            out var error);

        Assert.True(isValid);
        Assert.Null(error);
        Assert.NotNull(session);

        var firstEndpoint = DaemonSessionIpcTransportEndpointAdapter.Adapt(session);
        var secondEndpoint = DaemonSessionIpcTransportEndpointAdapter.Adapt(session);

        Assert.Same(firstEndpoint, secondEndpoint);
        Assert.Same(session.UnixSocketEndpointPath, firstEndpoint.UnixSocketPath);
        Assert.Equal(session.EndpointContract, firstEndpoint.Contract);
        Assert.Equal(firstEndpoint.UnixSocketPath!.Value, firstEndpoint.Contract.Address);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WhenUnixSocketAddressExceedsTransportLimit_ReturnsNoSession ()
    {
        var sourceSession = DaemonSessionTestFactory.Create();
        var contract = DaemonSessionContractMapper.ToContract(sourceSession) with
        {
            EndpointTransportKind = IpcTransportKind.UnixDomainSocket,
            EndpointAddress = Path.Combine(
                Path.GetTempPath(),
                new string('s', 120) + ".sock"),
        };

        var isValid = DaemonSessionIpcTransportEndpointAdapter.TryCreate(
            contract,
            sourceSession.ProjectFingerprint,
            "test session contract.",
            out var session,
            out var error);

        Assert.False(isValid);
        Assert.Null(session);
        Assert.Contains(
            "UTF-8 byte length",
            Assert.IsType<ExecutionError>(error).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Adapt_WithUnixSocketSession_ReusesGuardedRuntimeEndpoint ()
    {
        var session = DaemonSessionTestFactory.Create(
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: Path.Combine(Path.GetTempPath(), "ucli-session-adapter.sock"));

        var firstEndpoint = DaemonSessionIpcTransportEndpointAdapter.Adapt(session);
        var secondEndpoint = DaemonSessionIpcTransportEndpointAdapter.Adapt(session);

        Assert.Same(firstEndpoint, secondEndpoint);
        Assert.Same(session.UnixSocketEndpointPath, firstEndpoint.UnixSocketPath);
        Assert.Equal(session.EndpointContract, firstEndpoint.Contract);
    }
}

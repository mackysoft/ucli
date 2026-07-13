using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonSessionConnectionProviderTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Resolve_WhenSessionExists_ReturnsConnection ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-connection-provider", "session-exists");
        var store = DaemonSessionStorageTestSupport.CreateStore();
        var provider = new DaemonSessionConnectionProvider(store);
        var context = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
            scope.FullPath,
            projectFingerprint: "fingerprint-session-exists");

        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: context.ProjectFingerprint,
            sessionToken: "resolved-token",
            endpointTransportKind: "namedPipe",
            endpointAddress: "ucli-daemon-test");
        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);
        Assert.True(writeResult.IsSuccess);

        var resolveResult = await provider.ResolveAsync(context, CancellationToken.None);

        Assert.True(resolveResult.IsSuccess);
        Assert.Equal(
            IpcSessionTokenTestFactory.Create("resolved-token"),
            resolveResult.Connection!.SessionToken);
        Assert.Equal(IpcTransportKind.NamedPipe, resolveResult.Connection.Endpoint.TransportKind);
        Assert.Equal("ucli-daemon-test", resolveResult.Connection.Endpoint.Address);
        Assert.Null(resolveResult.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Resolve_WhenSessionDoesNotExist_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-connection-provider", "session-missing");
        var provider = new DaemonSessionConnectionProvider(DaemonSessionStorageTestSupport.CreateStore());
        var context = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
            scope.FullPath,
            projectFingerprint: "fingerprint-session-missing");

        var resolveResult = await provider.ResolveAsync(context, CancellationToken.None);

        Assert.False(resolveResult.IsSuccess);
        Assert.True(resolveResult.IsSessionNotAvailable);
        var error = Assert.IsType<ExecutionError>(resolveResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("not available", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}

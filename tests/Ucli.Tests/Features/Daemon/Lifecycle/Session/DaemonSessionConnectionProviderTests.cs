using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonSessionConnectionProviderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenSessionExists_ReturnsConnection ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-connection-provider", "session-exists");
        var store = new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator());
        var provider = new DaemonSessionConnectionProvider(store);
        var context = CreateContext(scope.FullPath, "fingerprint-session-exists");

        var session = CreateSession(context.ProjectFingerprint, "resolved-token");
        var writeResult = await store.WriteAsync(scope.FullPath, session, CancellationToken.None);
        Assert.True(writeResult.IsSuccess);

        var resolveResult = await provider.ResolveAsync(context, CancellationToken.None);

        Assert.True(resolveResult.IsSuccess);
        Assert.Equal("resolved-token", resolveResult.Connection!.SessionToken);
        Assert.Equal(IpcTransportKind.NamedPipe, resolveResult.Connection.Endpoint.TransportKind);
        Assert.Equal("ucli-daemon-test", resolveResult.Connection.Endpoint.Address);
        Assert.Null(resolveResult.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenSessionDoesNotExist_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-connection-provider", "session-missing");
        var provider = new DaemonSessionConnectionProvider(new DaemonSessionStore(new DaemonSessionJsonSerializer(), new DaemonSessionValidator()));
        var context = CreateContext(scope.FullPath, "fingerprint-session-missing");

        var resolveResult = await provider.ResolveAsync(context, CancellationToken.None);

        Assert.False(resolveResult.IsSuccess);
        Assert.True(resolveResult.IsSessionNotAvailable);
        var error = Assert.IsType<ExecutionError>(resolveResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("not available", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ResolvedUnityProjectContext CreateContext (
        string repositoryRoot,
        string projectFingerprint)
    {
        var unityProjectRoot = Path.Combine(repositoryRoot, "UnityProject");
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: unityProjectRoot,
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: projectFingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (
        string projectFingerprint,
        string sessionToken)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: sessionToken,
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: DateTimeOffset.UtcNow,
            EditorMode: "batchmode",
            OwnerKind: "cli",
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test",
            ProcessId: 123,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 9876);
    }
}

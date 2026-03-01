namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

public sealed class DaemonSessionTokenProviderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenSessionExists_ReturnsToken ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-token-provider", "session-exists");
        var store = new DaemonSessionStore();
        var provider = new DaemonSessionTokenProvider(store);
        var context = CreateContext(scope.FullPath, "fingerprint-session-exists");

        var session = CreateSession(context.ProjectFingerprint, "resolved-token");
        var writeResult = await store.Write(scope.FullPath, session, CancellationToken.None);
        Assert.True(writeResult.IsSuccess);

        var resolveResult = await provider.Resolve(context, CancellationToken.None);

        Assert.True(resolveResult.IsSuccess);
        Assert.Equal("resolved-token", resolveResult.Token);
        Assert.Null(resolveResult.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenSessionDoesNotExist_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-session-token-provider", "session-missing");
        var provider = new DaemonSessionTokenProvider(new DaemonSessionStore());
        var context = CreateContext(scope.FullPath, "fingerprint-session-missing");

        var resolveResult = await provider.Resolve(context, CancellationToken.None);

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
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindCli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-test",
            ProcessId: 123);
    }
}

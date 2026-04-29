using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Foundation;

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
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test",
            ProcessId: 123,

            OwnerProcessId: 9876);
    }
}

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonArtifactCleanerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenUnixFallbackSocketDirectoryBecomesEmpty_DeletesFallbackDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var socketPath = UnixSocketPathUtilities.BuildFallbackSocketPath(UcliIpcEndpointNames.DaemonAddressPrefix, Guid.NewGuid().ToString("N"));
        var socketDirectoryPath = Path.GetDirectoryName(socketPath)!;
        Directory.CreateDirectory(socketDirectoryPath);
        await File.WriteAllTextAsync(socketPath, string.Empty, CancellationToken.None);

        var cleaner = new DaemonArtifactCleaner(
            new StubDaemonSessionStore(),
            new StubDaemonLifecycleStore(),
            new StubDaemonLaunchAttemptStore(),
            new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.UnixDomainSocket, socketPath)));

        var result = await cleaner.CleanupAsync(
            new ResolvedUnityProjectContext(
                UnityProjectRoot: "/tmp/unity-project",
                RepositoryRoot: "/tmp/repo-root",
                ProjectFingerprint: "fingerprint-cleanup",
                PathSource: UnityProjectPathSource.CommandOption),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(socketPath));
        Assert.False(Directory.Exists(socketDirectoryPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenLaunchAttemptStoreDeletesOldAttempts_ReturnsDeletedLaunchAttemptCount ()
    {
        var launchAttemptStore = new StubDaemonLaunchAttemptStore
        {
            PruneResult = DaemonLaunchAttemptStoreOperationResult.Success(deletedCount: 3),
        };
        var cleaner = new DaemonArtifactCleaner(
            new StubDaemonSessionStore(),
            new StubDaemonLifecycleStore(),
            launchAttemptStore,
            new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test-pipe")));

        var result = await cleaner.CleanupAsync(
            new ResolvedUnityProjectContext(
                UnityProjectRoot: "/tmp/unity-project",
                RepositoryRoot: "/tmp/repo-root",
                ProjectFingerprint: "fingerprint-cleanup",
                PathSource: UnityProjectPathSource.CommandOption),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.DeletedLaunchAttemptCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenLaunchAttemptPruneFails_ReturnsFailure ()
    {
        var pruneError = ExecutionError.InternalError("prune failed");
        var cleaner = new DaemonArtifactCleaner(
            new StubDaemonSessionStore(),
            new StubDaemonLifecycleStore(),
            new StubDaemonLaunchAttemptStore
            {
                PruneResult = DaemonLaunchAttemptStoreOperationResult.Failure(pruneError),
            },
            new StubEndpointResolver(new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-test-pipe")));

        var result = await cleaner.CleanupAsync(
            new ResolvedUnityProjectContext(
                UnityProjectRoot: "/tmp/unity-project",
                RepositoryRoot: "/tmp/repo-root",
                ProjectFingerprint: "fingerprint-cleanup",
                PathSource: UnityProjectPathSource.CommandOption),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(pruneError, result.Error);
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonLifecycleStore : IDaemonLifecycleStore
    {
        public ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonLifecycleStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonLaunchAttemptStore : IDaemonLaunchAttemptStore
    {
        public DaemonLaunchAttemptStoreOperationResult PruneResult { get; init; } = DaemonLaunchAttemptStoreOperationResult.Success();

        public ValueTask<DaemonLaunchAttemptStoreOperationResult> WriteFailureAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonLaunchAttempt launchAttempt,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonLaunchAttemptReadResult> ReadLastFailureAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonLaunchAttemptStoreOperationResult> PruneAsync (
            string storageRoot,
            string projectFingerprint,
            int keepCount,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(PruneResult);
        }
    }

    private sealed class StubEndpointResolver : IIpcEndpointResolver
    {
        private readonly IpcEndpoint endpoint;

        public StubEndpointResolver (IpcEndpoint endpoint)
        {
            this.endpoint = endpoint;
        }

        public IpcEndpoint Resolve (
            string storageRoot,
            string projectFingerprint)
        {
            return endpoint;
        }
    }
}

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonArtifactCleanerTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Cleanup_WhenUnixFallbackSocketDirectoryBecomesEmpty_DeletesFallbackDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-artifact-cleaner", "fallback-socket");
        var storageRoot = Path.Combine(scope.FullPath, new string('a', 160), new string('b', 160));
        const string projectFingerprint = "fingerprint-cleanup";
        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, projectFingerprint);
        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint.TransportKind);

        var socketPath = endpoint.Address;
        var socketDirectoryPath = Path.GetDirectoryName(socketPath)!;
        Directory.CreateDirectory(socketDirectoryPath);
        await File.WriteAllTextAsync(socketPath, string.Empty, CancellationToken.None);

        var cleaner = new DaemonArtifactCleaner(
            new RecordingDaemonSessionStore(),
            new RecordingDaemonLifecycleStore(),
            new RecordingDaemonLaunchAttemptStore());

        var result = await cleaner.CleanupAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: storageRoot,
                projectFingerprint: projectFingerprint),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(socketPath));
        Assert.False(Directory.Exists(socketDirectoryPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenLaunchAttemptStoreDeletesOldAttempts_ReturnsDeletedLaunchAttemptCount ()
    {
        var launchAttemptStore = new RecordingDaemonLaunchAttemptStore
        {
            PruneResult = DaemonLaunchAttemptStoreOperationResult.Success(deletedCount: 3),
        };
        var cleaner = new DaemonArtifactCleaner(
            new RecordingDaemonSessionStore(),
            new RecordingDaemonLifecycleStore(),
            launchAttemptStore);

        var result = await cleaner.CleanupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup"),
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
            new RecordingDaemonSessionStore(),
            new RecordingDaemonLifecycleStore(),
            new RecordingDaemonLaunchAttemptStore
            {
                PruneResult = DaemonLaunchAttemptStoreOperationResult.Failure(pruneError),
            });

        var result = await cleaner.CleanupAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(pruneError, result.Error);
    }

}

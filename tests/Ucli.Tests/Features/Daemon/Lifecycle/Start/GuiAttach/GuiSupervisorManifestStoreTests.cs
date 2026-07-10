using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiAttach;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Daemon.DaemonGuiRebootstrapClientTestSupport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class GuiSupervisorManifestStoreTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAfterEndpointPublication_WhenPublicationLockIsHeld_WaitsForPublishedManifest ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "gui-supervisor-manifest-store",
            "consistent-publication-read");
        const string ProjectFingerprint = "fingerprint";
        var manifest = CreateManifest();
        var manifestLockPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestLockPath(
            scope.FullPath,
            ProjectFingerprint);
        using var publicationLock = FileExclusiveLock.Acquire(
            manifestLockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var store = new GuiSupervisorManifestStore();

        var readTask = store.ReadAfterEndpointPublicationAsync(
                scope.FullPath,
                ProjectFingerprint,
                TimeSpan.FromSeconds(1),
                CancellationToken.None)
            .AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        Assert.False(readTask.IsCompleted);

        await WriteManifestAsync(scope.FullPath, ProjectFingerprint, manifest);
        publicationLock.Dispose();
        var result = await readTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(manifest, result);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAfterEndpointPublication_WhenLockWaitExceedsTimeout_ThrowsTimeoutException ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "gui-supervisor-manifest-store",
            "publication-read-timeout");
        const string ProjectFingerprint = "fingerprint";
        var manifestLockPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestLockPath(
            scope.FullPath,
            ProjectFingerprint);
        using var publicationLock = FileExclusiveLock.Acquire(
            manifestLockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var store = new GuiSupervisorManifestStore();

        await Assert.ThrowsAsync<TimeoutException>(() => store.ReadAfterEndpointPublicationAsync(
                scope.FullPath,
                ProjectFingerprint,
                TimeSpan.FromMilliseconds(50),
                CancellationToken.None)
            .AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAfterEndpointPublication_WhenCallerCancels_PropagatesCancellation ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "gui-supervisor-manifest-store",
            "publication-read-cancellation");
        const string ProjectFingerprint = "fingerprint";
        var manifestLockPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestLockPath(
            scope.FullPath,
            ProjectFingerprint);
        using var publicationLock = FileExclusiveLock.Acquire(
            manifestLockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        using var cancellationTokenSource = new CancellationTokenSource();
        var store = new GuiSupervisorManifestStore();

        var readTask = store.ReadAfterEndpointPublicationAsync(
                scope.FullPath,
                ProjectFingerprint,
                TimeSpan.FromSeconds(1),
                cancellationTokenSource.Token)
            .AsTask();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readTask);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrap_WhenPublicationLockWaitTimesOut_ReturnsStructuredTimeout ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "gui-supervisor-manifest-store",
            "rebootstrap-publication-timeout");
        const string ProjectFingerprint = "fingerprint";
        var manifestLockPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestLockPath(
            scope.FullPath,
            ProjectFingerprint);
        using var publicationLock = FileExclusiveLock.Acquire(
            manifestLockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException(
                "Manifest publication timeout must not send IPC."),
        };
        var client = new DaemonGuiRebootstrapClient(
            new GuiSupervisorManifestStore(),
            transportClient,
            TimeProvider.System);
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
            scope.FullPath,
            ProjectFingerprint);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            expectedProcessId: 1234,
            expectedProcessStartedAtUtc: null,
            TimeSpan.FromMilliseconds(50),
            CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Empty(transportClient.Invocations);
    }
}

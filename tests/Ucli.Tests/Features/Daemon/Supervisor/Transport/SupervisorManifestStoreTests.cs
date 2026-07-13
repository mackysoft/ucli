using System.Runtime.Versioning;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorManifestStoreTests
{
    private static readonly TimeSpan AsyncTestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOrNull_WhenReadIgnoresCancellation_ReturnsAtDeadline ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "noncooperative-read-timeout");
        var timeProvider = new ManualTimeProvider();
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCompletion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationCallbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationCallbackCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var allowCancellationCallbackCompletion = new ManualResetEventSlim();
        var store = new SupervisorManifestStore(
            timeProvider,
            (manifestPath, cancellationToken) =>
            {
                _ = manifestPath;
                _ = cancellationToken.Register(() =>
                {
                    cancellationCallbackStarted.TrySetResult();
                    allowCancellationCallbackCompletion.Wait();
                    cancellationCallbackCompleted.TrySetResult();
                });
                readStarted.TrySetResult();
                return new ValueTask<string?>(readCompletion.Task);
            },
            static (_, _, _) => ValueTask.CompletedTask,
            static _ => { });
        var timeout = TimeSpan.FromSeconds(1);

        var readTask = store.ReadOrNullAsync(scope.FullPath, timeout, CancellationToken.None).AsTask();
        await readStarted.Task.WaitAsync(AsyncTestTimeout);
        await timeProvider
            .WaitForTimerDueWithinAsync(timeout)
            .WaitAsync(AsyncTestTimeout);

        Task? advanceTask = null;
        try
        {
            advanceTask = Task.Run(() => timeProvider.Advance(timeout));
            await cancellationCallbackStarted.Task.WaitAsync(AsyncTestTimeout);
            var completedTask = await Task.WhenAny(readTask, Task.Delay(AsyncTestTimeout));
            Assert.Same(readTask, completedTask);
            var exception = await Assert.ThrowsAsync<TimeoutException>(() => readTask);

            Assert.Contains("Timed out while reading supervisor manifest", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            allowCancellationCallbackCompletion.Set();
            if (advanceTask is not null)
            {
                await advanceTask.WaitAsync(AsyncTestTimeout);
            }

            await cancellationCallbackCompleted.Task.WaitAsync(AsyncTestTimeout);
            readCompletion.TrySetResult(null);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteReadDelete_RoundTripsManifestJson ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "roundtrip");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpoint = new SupervisorEndpointResolver().ResolveCanonicalEndpoint(scope.FullPath);
        var manifest = CreateManifest(endpoint: endpoint);

        await store.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        var loadedManifest = await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None);

        Assert.NotNull(loadedManifest);
        Assert.Equal(manifest.ProcessId, loadedManifest.ProcessId);
        Assert.Equal(manifest.SessionToken, loadedManifest.SessionToken);
        Assert.Equal(manifest.Endpoint, loadedManifest.Endpoint);
        Assert.Equal(manifest.IssuedAtUtc, loadedManifest.IssuedAtUtc);

        var cleanupStatus = await store.CleanupRuntimeIfManifestMatchesAsync(
            scope.FullPath,
            manifest,
            endpoint,
            SupervisorConstants.ManifestMutationLockTimeout,
            CancellationToken.None);

        var readAfterDelete = await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None);
        Assert.Equal(SupervisorManifestCleanupStatus.Removed, cleanupStatus);
        Assert.Null(readAfterDelete);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenSessionTokenIsNotCanonical_RejectsManifest ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "supervisor-manifest-store",
            "noncanonical-session-token");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var manifest = CreateManifest();
        var contract = new SupervisorInstanceManifestJsonContract(
            manifest.ProcessId,
            "legacy-token",
            ContractLiteralCodec.ToValue(manifest.Endpoint.TransportKind),
            manifest.Endpoint.Address,
            manifest.IssuedAtUtc);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(contract),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<SupervisorManifestFormatException>(
            () => store.ReadOrNullAsync(scope.FullPath, CancellationToken.None).AsTask());

        Assert.IsType<InvalidDataException>(exception.InnerException);
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public async Task CleanupRuntime_OnUnix_RemovesPublishedGenerationSocket ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope(
            "supervisor-manifest-store",
            "published-generation-cleanup");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpoint = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            UnixSocketPathUtilities.BuildFallbackSocketPath(
                UcliIpcEndpointNames.SupervisorAddressPrefix,
                Guid.NewGuid().ToString("N")));
        var endpointOwnership = new SupervisorUnixSocketEndpointOwnership(endpoint.Address);
        endpointOwnership.PrepareForBind();
        await File.WriteAllTextAsync(endpointOwnership.BoundAddress, "generation socket", CancellationToken.None);
        endpointOwnership.PublishBoundEndpoint();
        var generationDirectoryPath = Path.GetDirectoryName(endpointOwnership.BoundAddress)!;
        var canonicalDirectoryPath = Path.GetDirectoryName(endpoint.Address)!;
        var manifest = CreateManifest(endpoint: endpoint);
        await store.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        var cleanupStatus = await store.CleanupRuntimeIfManifestMatchesAsync(
            scope.FullPath,
            manifest,
            endpoint,
            SupervisorConstants.ManifestMutationLockTimeout,
            CancellationToken.None);

        Assert.Equal(SupervisorManifestCleanupStatus.Removed, cleanupStatus);
        Assert.Null(new FileInfo(endpoint.Address).LinkTarget);
        Assert.False(File.Exists(endpoint.Address));
        Assert.False(File.Exists(endpointOwnership.BoundAddress));
        Assert.False(Directory.Exists(generationDirectoryPath));
        Assert.False(Directory.Exists(canonicalDirectoryPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupMalformedRuntime_WhenManifestWasReplaced_PreservesSuccessorGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "malformed-generation-replaced");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpoint = new SupervisorEndpointResolver().ResolveCanonicalEndpoint(scope.FullPath);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(manifestPath, "{ malformed json", CancellationToken.None);
        var formatException = await Assert.ThrowsAsync<SupervisorManifestFormatException>(
            () => store.ReadOrNullAsync(scope.FullPath, CancellationToken.None).AsTask());
        var originalManifest = CreateManifest();
        var successorManifest = CreateManifest(
            sessionTokenDiscriminator: 2,
            endpoint: endpoint,
            issuedAtUtc: originalManifest.IssuedAtUtc.AddSeconds(1));
        await store.WriteAsync(scope.FullPath, successorManifest, CancellationToken.None);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(endpoint.Address)!);
            await File.WriteAllTextAsync(endpoint.Address, "successor endpoint", CancellationToken.None);
        }

        var cleanupStatus = await store.CleanupRuntimeIfMalformedArtifactMatchesAsync(
            scope.FullPath,
            formatException.ArtifactIdentity,
            endpoint,
            SupervisorConstants.ManifestMutationLockTimeout,
            CancellationToken.None);

        Assert.Equal(SupervisorManifestCleanupStatus.GenerationMismatch, cleanupStatus);
        Assert.Equal(
            successorManifest,
            await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None));
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.True(File.Exists(endpoint.Address));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EndpointPublicationLease_BlocksOldGenerationCleanupUntilSuccessorManifestIsPublished ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "endpoint-publication-race");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpoint = new SupervisorEndpointResolver().ResolveCanonicalEndpoint(scope.FullPath);
        var firstManifest = CreateManifest(endpoint: endpoint);
        var successorManifest = CreateManifest(
            sessionTokenDiscriminator: 2,
            endpoint: firstManifest.Endpoint,
            issuedAtUtc: firstManifest.IssuedAtUtc.AddSeconds(1));
        await store.WriteAsync(scope.FullPath, firstManifest, CancellationToken.None);
        using var publicationLease = await store.AcquireEndpointPublicationLeaseAsync(
            scope.FullPath,
            AsyncTestTimeout,
            CancellationToken.None);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(endpoint.Address)!);
            await File.WriteAllTextAsync(endpoint.Address, "bound successor endpoint", CancellationToken.None);
        }

        var cleanupTask = store.CleanupRuntimeIfManifestMatchesAsync(
                scope.FullPath,
                firstManifest,
                endpoint,
                AsyncTestTimeout,
                CancellationToken.None)
            .AsTask();
        Assert.False(cleanupTask.IsCompleted);

        await publicationLease.PublishAsync(successorManifest, CancellationToken.None);
        publicationLease.Dispose();
        var cleanupStatus = await cleanupTask.WaitAsync(AsyncTestTimeout);

        Assert.Equal(SupervisorManifestCleanupStatus.GenerationMismatch, cleanupStatus);
        Assert.Equal(
            successorManifest,
            await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None));
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.True(File.Exists(endpoint.Address));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAfterEndpointPublication_WhenSuccessorOwnsPublicationLease_WaitsForSuccessorManifest ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "supervisor-manifest-store",
            "consistent-successor-read");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(new ManualTimeProvider());
        var initialManifest = CreateManifest();
        var successorManifest = CreateManifest(
            sessionTokenDiscriminator: 2,
            issuedAtUtc: initialManifest.IssuedAtUtc.AddSeconds(1));
        await store.WriteAsync(scope.FullPath, initialManifest, CancellationToken.None);
        using var publicationLease = await store.AcquireEndpointPublicationLeaseAsync(
            scope.FullPath,
            AsyncTestTimeout,
            CancellationToken.None);

        var readTask = store.ReadAfterEndpointPublicationAsync(
                scope.FullPath,
                AsyncTestTimeout,
                CancellationToken.None)
            .AsTask();
        Assert.False(readTask.IsCompleted);

        await publicationLease.PublishAsync(successorManifest, CancellationToken.None);
        publicationLease.Dispose();
        var manifest = await readTask.WaitAsync(AsyncTestTimeout);

        Assert.Equal(successorManifest, manifest);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EndpointPublicationLease_WhenAtomicSuccessorWriteThrows_RestoresReplacedManifestBeforeFailureReturns ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "supervisor-manifest-store",
            "publication-write-rollback");
        var fileBackedStore = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var replacedManifest = CreateManifest();
        var successorManifest = CreateManifest(
            sessionTokenDiscriminator: 2,
            issuedAtUtc: replacedManifest.IssuedAtUtc.AddSeconds(1));
        await fileBackedStore.WriteAsync(scope.FullPath, replacedManifest, CancellationToken.None);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        var replacedManifestJson = await File.ReadAllTextAsync(manifestPath, CancellationToken.None);
        var writeCount = 0;
        var store = new SupervisorManifestStore(
            TimeProvider.System,
            static (path, cancellationToken) =>
                FileUtilities.ReadAllTextOrNullAsync(path, cancellationToken),
            async (path, contents, cancellationToken) =>
            {
                await FileUtilities.WriteAllTextAtomicallyAsync(path, contents, cancellationToken);
                if (Interlocked.Increment(ref writeCount) == 1)
                {
                    throw new IOException("Injected failure after atomic replacement.");
                }
            },
            static path => FileUtilities.DeleteIfExists(path));
        using var publicationLease = await store.AcquireEndpointPublicationLeaseAsync(
            scope.FullPath,
            SupervisorConstants.ManifestMutationLockTimeout,
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<IOException>(
            async () => await publicationLease.PublishAsync(successorManifest, CancellationToken.None));

        Assert.Contains("after atomic replacement", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, writeCount);
        Assert.Equal(
            replacedManifestJson,
            await File.ReadAllTextAsync(manifestPath, CancellationToken.None));
        Assert.Equal(
            replacedManifest,
            await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public async Task Write_OnUnix_SavesManifestJsonUnderOwnerOnlyBoundary ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "owner-only");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var manifest = CreateManifest();

        await store.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        var localDirectoryPath = UcliStoragePathResolver.ResolveLocalDirectoryPath(scope.FullPath);
        var supervisorDirectoryPath = UcliStoragePathResolver.ResolveSupervisorDirectoryPath(scope.FullPath);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);

        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(localDirectoryPath);
        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(supervisorDirectoryPath);
        PosixAccessBoundaryAssert.FileIsOwnerOnly(manifestPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("windows")]
    public async Task Write_OnWindows_SavesManifestJsonUnderCurrentUserOnlyBoundary ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "current-user-only");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var manifest = CreateManifest();

        await store.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        var localDirectoryPath = UcliStoragePathResolver.ResolveLocalDirectoryPath(scope.FullPath);
        var supervisorDirectoryPath = UcliStoragePathResolver.ResolveSupervisorDirectoryPath(scope.FullPath);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);

        WindowsAccessBoundaryAssert.DirectoryIsCurrentUserOnly(localDirectoryPath);
        WindowsAccessBoundaryAssert.DirectoryIsCurrentUserOnly(supervisorDirectoryPath);
        WindowsAccessBoundaryAssert.FileIsCurrentUserOnly(manifestPath);
    }

    private static SupervisorInstanceManifest CreateManifest (
        byte sessionTokenDiscriminator = 1,
        IpcEndpoint? endpoint = null,
        DateTimeOffset? issuedAtUtc = null)
    {
        return new SupervisorInstanceManifest(
            processId: 2468,
            sessionToken: IpcSessionTokenTestFactory.CreateFromDiscriminator(sessionTokenDiscriminator),
            endpoint: endpoint ?? new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-supervisor-test/ipc.sock"),
            issuedAtUtc: issuedAtUtc ?? new DateTimeOffset(2026, 03, 14, 0, 0, 0, TimeSpan.Zero));
    }
}

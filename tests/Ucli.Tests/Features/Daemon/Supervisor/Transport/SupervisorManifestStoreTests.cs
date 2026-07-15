using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
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
        var readCompletion = new TaskCompletionSource<ReadOnlyMemory<byte>?>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                return new ValueTask<ReadOnlyMemory<byte>?>(readCompletion.Task);
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
        var endpointResolver = new SupervisorEndpointResolver();
        var endpoint = endpointResolver.ResolveRuntimeEndpoint(
            scope.FullPath,
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1));
        var cleanupTarget = endpointResolver.ResolveUnixSocketCleanupTargetOrNull(scope.FullPath);
        var manifest = CreateManifest(endpoint: endpoint);

        await store.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        var loadedManifest = await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None);

        Assert.NotNull(loadedManifest);
        Assert.Equal(manifest.ProcessId, loadedManifest.ProcessId);
        Assert.Equal(manifest.SessionToken, loadedManifest.SessionToken);
        Assert.Equal(manifest.Endpoint, loadedManifest.Endpoint);
        Assert.Equal(manifest.IssuedAtUtc, loadedManifest.IssuedAtUtc);

        var cleanupStatus = await store.CleanupObservedRuntimeIfManifestMatchesAsync(
            scope.FullPath,
            manifest,
            cleanupTarget,
            SupervisorConstants.ManifestMutationLockTimeout,
            CancellationToken.None);

        var readAfterDelete = await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None);
        Assert.Equal(SupervisorManifestCleanupStatus.Removed, cleanupStatus);
        Assert.Null(readAfterDelete);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenManifestIsMalformed_ReportsExactSha256Digest ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "malformed-content-identity");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        const string malformedJson = "{ malformed json";
        await File.WriteAllTextAsync(manifestPath, malformedJson, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<SupervisorManifestFormatException>(
            () => store.ReadOrNullAsync(scope.FullPath, CancellationToken.None).AsTask());

        Assert.Equal(
            Sha256Digest.Compute(Encoding.UTF8.GetBytes(malformedJson)),
            exception.ArtifactIdentity);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenManifestContainsInvalidUtf8_ReportsDigestOfExactRawBytes ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "invalid-utf8-identity");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        byte[] invalidUtf8 = [0x7B, 0xFF, 0x7D];
        await File.WriteAllBytesAsync(manifestPath, invalidUtf8, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<SupervisorManifestFormatException>(
            () => store.ReadOrNullAsync(scope.FullPath, CancellationToken.None).AsTask());

        Assert.IsType<DecoderFallbackException>(exception.InnerException);
        Assert.Equal(Sha256Digest.Compute(invalidUtf8), exception.ArtifactIdentity);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenManifestIsUtf16_RejectsEncodingAndReportsDigestOfExactRawBytes ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "utf16-identity");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var manifest = CreateManifest();
        var manifestJson = SupervisorManifestStoreTestSupport.Serialize(manifest);
        var utf16Bytes = CombineBytes(
            Encoding.Unicode.GetPreamble(),
            Encoding.Unicode.GetBytes(manifestJson));
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllBytesAsync(manifestPath, utf16Bytes, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<SupervisorManifestFormatException>(
            () => store.ReadOrNullAsync(scope.FullPath, CancellationToken.None).AsTask());

        Assert.IsType<DecoderFallbackException>(exception.InnerException);
        Assert.Equal(Sha256Digest.Compute(utf16Bytes), exception.ArtifactIdentity);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenMalformedManifestHasUtf8Bom_ReportsDigestIncludingBom ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "utf8-bom-identity");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var decodedBytes = Encoding.UTF8.GetBytes("{ malformed json");
        var rawBytes = CombineBytes(Encoding.UTF8.GetPreamble(), decodedBytes);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllBytesAsync(manifestPath, rawBytes, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<SupervisorManifestFormatException>(
            () => store.ReadOrNullAsync(scope.FullPath, CancellationToken.None).AsTask());

        Assert.Equal(Sha256Digest.Compute(rawBytes), exception.ArtifactIdentity);
        Assert.NotEqual(Sha256Digest.Compute(decodedBytes), exception.ArtifactIdentity);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Read_WhenValidManifestHasSingleUtf8Bom_AcceptsManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "valid-utf8-bom");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var expectedManifest = CreateManifest();
        var rawBytes = CombineBytes(
            Encoding.UTF8.GetPreamble(),
            Encoding.UTF8.GetBytes(SupervisorManifestStoreTestSupport.Serialize(expectedManifest)));
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllBytesAsync(manifestPath, rawBytes, CancellationToken.None);

        var manifest = await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None);

        Assert.Equal(expectedManifest, manifest);
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
    public async Task CleanupObservedRuntime_OnUnix_RemovesPublishedGenerationSocket ()
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
            new UnixSocketFallbackPath(
                Path.GetTempPath(),
                UnixSocketFallbackPurpose.Supervisor,
                Guid.NewGuid().ToString("N")).SocketPath);
        var endpointOwnership = new SupervisorUnixSocketEndpointOwnership(endpoint.Address);
        endpointOwnership.PrepareForBind();
        await File.WriteAllTextAsync(endpointOwnership.BoundAddress, "generation socket", CancellationToken.None);
        endpointOwnership.PublishBoundEndpoint();
        var generationDirectoryPath = Path.GetDirectoryName(endpointOwnership.BoundAddress)!;
        var canonicalDirectoryPath = Path.GetDirectoryName(endpoint.Address)!;
        var manifest = CreateManifest(endpoint: endpoint);
        await store.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        try
        {
            var cleanupStatus = await store.CleanupObservedRuntimeIfManifestMatchesAsync(
                scope.FullPath,
                manifest,
                new SupervisorUnixSocketCleanupTarget(endpoint.Address),
                SupervisorConstants.ManifestMutationLockTimeout,
                CancellationToken.None);

            Assert.Equal(SupervisorManifestCleanupStatus.Removed, cleanupStatus);
            Assert.Null(new FileInfo(endpoint.Address).LinkTarget);
            Assert.False(File.Exists(endpoint.Address));
            Assert.False(File.Exists(endpointOwnership.BoundAddress));
            Assert.True(Directory.Exists(generationDirectoryPath));
            Assert.True(Directory.Exists(canonicalDirectoryPath));

            endpointOwnership.Cleanup();

            Assert.False(Directory.Exists(generationDirectoryPath));
            Assert.True(Directory.Exists(canonicalDirectoryPath));
        }
        finally
        {
            endpointOwnership.Cleanup();
            if (Directory.Exists(canonicalDirectoryPath))
            {
                Directory.Delete(canonicalDirectoryPath, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupObservedRuntime_WithoutUnixSocketTarget_DeletesOnlyManifestArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "manifest-only-cleanup");
        var deletedPaths = new List<string>();
        var store = new SupervisorManifestStore(
            TimeProvider.System,
            static (path, cancellationToken) => FileUtilities.ReadAllBytesOrNullAsync(path, cancellationToken),
            static (path, contents, cancellationToken) => FileUtilities.WriteAllBytesAtomicallyAsync(path, contents, cancellationToken),
            path =>
            {
                deletedPaths.Add(path);
                FileUtilities.DeleteIfExists(path);
            });
        var manifest = CreateManifest(
            endpoint: new IpcEndpoint(IpcTransportKind.NamedPipe, $"ucli-supervisor-{Guid.NewGuid():N}"));
        await store.WriteAsync(scope.FullPath, manifest, CancellationToken.None);

        var cleanupStatus = await store.CleanupObservedRuntimeIfManifestMatchesAsync(
            scope.FullPath,
            manifest,
            unixSocketCleanupTarget: null,
            timeout: SupervisorConstants.ManifestMutationLockTimeout,
            cancellationToken: CancellationToken.None);

        Assert.Equal(SupervisorManifestCleanupStatus.Removed, cleanupStatus);
        Assert.Collection(
            deletedPaths,
            path => Assert.Equal(UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath), path));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupObservedValidRuntime_WhenSuccessorReplacesManifestWhileOwnershipIsContended_PreservesSuccessorRuntime ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "valid-generation-replaced");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpointResolver = new SupervisorEndpointResolver();
        var endpoint = endpointResolver.ResolveRuntimeEndpoint(
            scope.FullPath,
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1));
        var cleanupTarget = endpointResolver.ResolveUnixSocketCleanupTargetOrNull(scope.FullPath);
        var observedManifest = CreateManifest(endpoint: endpoint);
        var successorManifest = CreateManifest(
            sessionTokenDiscriminator: 2,
            endpoint: endpoint,
            issuedAtUtc: observedManifest.IssuedAtUtc.AddSeconds(1));
        await store.WriteAsync(scope.FullPath, observedManifest, CancellationToken.None);
        using var runtimeOwnership = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(scope.FullPath),
            AsyncTestTimeout,
            CancellationToken.None);
        var cleanupTask = store.CleanupObservedRuntimeIfManifestMatchesAsync(
                scope.FullPath,
                observedManifest,
                cleanupTarget,
                AsyncTestTimeout,
                CancellationToken.None)
            .AsTask();
        Assert.False(cleanupTask.IsCompleted);

        await store.WriteAsync(scope.FullPath, successorManifest, CancellationToken.None);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(endpoint.Address)!);
            await File.WriteAllTextAsync(endpoint.Address, "successor endpoint", CancellationToken.None);
        }

        runtimeOwnership.Dispose();
        var cleanupStatus = await cleanupTask.WaitAsync(AsyncTestTimeout);

        Assert.Equal(SupervisorManifestCleanupStatus.GenerationMismatch, cleanupStatus);
        Assert.Equal(successorManifest, await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None));
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.True(File.Exists(endpoint.Address));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupObservedValidRuntime_AfterRuntimeOwnershipIsReleased_RemovesExactRuntime ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "released-valid-runtime");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpointResolver = new SupervisorEndpointResolver();
        var endpoint = endpointResolver.ResolveRuntimeEndpoint(
            scope.FullPath,
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1));
        var cleanupTarget = endpointResolver.ResolveUnixSocketCleanupTargetOrNull(scope.FullPath);
        var manifest = CreateManifest(endpoint: endpoint);
        await store.WriteAsync(scope.FullPath, manifest, CancellationToken.None);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(endpoint.Address)!);
            await File.WriteAllTextAsync(endpoint.Address, "stale endpoint", CancellationToken.None);
        }

        using var runtimeOwnership = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(scope.FullPath),
            AsyncTestTimeout,
            CancellationToken.None);
        runtimeOwnership.Dispose();

        var cleanupStatus = await store.CleanupObservedRuntimeIfManifestMatchesAsync(
            scope.FullPath,
            manifest,
            cleanupTarget,
            AsyncTestTimeout,
            CancellationToken.None);

        Assert.Equal(SupervisorManifestCleanupStatus.Removed, cleanupStatus);
        Assert.Null(await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None));
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.False(File.Exists(endpoint.Address));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupObservedValidRuntime_WhenCanceledWhileWaitingForOwnership_PreservesRuntime ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "canceled-valid-cleanup");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpointResolver = new SupervisorEndpointResolver();
        var endpoint = endpointResolver.ResolveRuntimeEndpoint(
            scope.FullPath,
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1));
        var cleanupTarget = endpointResolver.ResolveUnixSocketCleanupTargetOrNull(scope.FullPath);
        var manifest = CreateManifest(endpoint: endpoint);
        await store.WriteAsync(scope.FullPath, manifest, CancellationToken.None);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(endpoint.Address)!);
            await File.WriteAllTextAsync(endpoint.Address, "owned endpoint", CancellationToken.None);
        }

        using var runtimeOwnership = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(scope.FullPath),
            AsyncTestTimeout,
            CancellationToken.None);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cleanupTask = store.CleanupObservedRuntimeIfManifestMatchesAsync(
                scope.FullPath,
                manifest,
                cleanupTarget,
                AsyncTestTimeout,
                cancellationTokenSource.Token)
            .AsTask();
        Assert.False(cleanupTask.IsCompleted);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cleanupTask);
        Assert.Equal(manifest, await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None));
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.True(File.Exists(endpoint.Address));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupObservedMalformedRuntime_WhenSuccessorReplacesArtifactWhileOwnershipIsContended_PreservesSuccessorGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "malformed-generation-replaced");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpointResolver = new SupervisorEndpointResolver();
        var endpoint = endpointResolver.ResolveRuntimeEndpoint(
            scope.FullPath,
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1));
        var cleanupTarget = endpointResolver.ResolveUnixSocketCleanupTargetOrNull(scope.FullPath);
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
        using var runtimeOwnership = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(scope.FullPath),
            AsyncTestTimeout,
            CancellationToken.None);
        var cleanupTask = store.CleanupObservedRuntimeIfMalformedArtifactMatchesAsync(
                scope.FullPath,
                formatException.ArtifactIdentity,
                cleanupTarget,
                AsyncTestTimeout,
                CancellationToken.None)
            .AsTask();
        Assert.False(cleanupTask.IsCompleted);

        await store.WriteAsync(scope.FullPath, successorManifest, CancellationToken.None);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(endpoint.Address)!);
            await File.WriteAllTextAsync(endpoint.Address, "successor endpoint", CancellationToken.None);
        }

        runtimeOwnership.Dispose();
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
    public async Task CleanupObservedMalformedRuntime_WhenWaitingArtifactChangesOnlyByUtf8Bom_PreservesReplacement ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "malformed-bom-replaced");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpointResolver = new SupervisorEndpointResolver();
        var cleanupTarget = endpointResolver.ResolveUnixSocketCleanupTargetOrNull(scope.FullPath);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var observedBytes = Encoding.UTF8.GetBytes("{ malformed json");
        var replacementBytes = CombineBytes(Encoding.UTF8.GetPreamble(), observedBytes);
        await File.WriteAllBytesAsync(manifestPath, observedBytes, CancellationToken.None);
        var formatException = await Assert.ThrowsAsync<SupervisorManifestFormatException>(
            () => store.ReadOrNullAsync(scope.FullPath, CancellationToken.None).AsTask());
        using var runtimeOwnership = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(scope.FullPath),
            AsyncTestTimeout,
            CancellationToken.None);
        var cleanupTask = store.CleanupObservedRuntimeIfMalformedArtifactMatchesAsync(
                scope.FullPath,
                formatException.ArtifactIdentity,
                cleanupTarget,
                AsyncTestTimeout,
                CancellationToken.None)
            .AsTask();
        Assert.False(cleanupTask.IsCompleted);

        await File.WriteAllBytesAsync(manifestPath, replacementBytes, CancellationToken.None);
        runtimeOwnership.Dispose();
        var cleanupStatus = await cleanupTask.WaitAsync(AsyncTestTimeout);

        Assert.Equal(SupervisorManifestCleanupStatus.GenerationMismatch, cleanupStatus);
        Assert.Equal(replacementBytes, await File.ReadAllBytesAsync(manifestPath, CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupObservedMalformedRuntime_WhenRuntimeOwnershipIsHeld_TimesOutWithoutDeletingArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "owned-malformed-runtime");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpointResolver = new SupervisorEndpointResolver();
        var cleanupTarget = endpointResolver.ResolveUnixSocketCleanupTargetOrNull(scope.FullPath);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        const string malformedJson = "{ malformed json";
        await File.WriteAllTextAsync(manifestPath, malformedJson, CancellationToken.None);
        if (cleanupTarget is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cleanupTarget.SocketPath)!);
            await File.WriteAllTextAsync(cleanupTarget.SocketPath, "owned endpoint", CancellationToken.None);
        }

        var formatException = await Assert.ThrowsAsync<SupervisorManifestFormatException>(
            () => store.ReadOrNullAsync(scope.FullPath, CancellationToken.None).AsTask());
        using var runtimeOwnership = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(scope.FullPath),
            AsyncTestTimeout,
            CancellationToken.None);

        await Assert.ThrowsAsync<TimeoutException>(
            () => store.CleanupObservedRuntimeIfMalformedArtifactMatchesAsync(
                    scope.FullPath,
                    formatException.ArtifactIdentity,
                    cleanupTarget,
                    TimeSpan.FromMilliseconds(100),
                    CancellationToken.None)
                .AsTask());

        Assert.Equal(malformedJson, await File.ReadAllTextAsync(manifestPath, CancellationToken.None));
        if (cleanupTarget is not null)
        {
            Assert.True(File.Exists(cleanupTarget.SocketPath));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupObservedMalformedRuntime_AfterRuntimeOwnershipLeaseIsReleased_RemovesStaleArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "released-malformed-runtime");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpointResolver = new SupervisorEndpointResolver();
        var cleanupTarget = endpointResolver.ResolveUnixSocketCleanupTargetOrNull(scope.FullPath);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(manifestPath, "{ malformed json", CancellationToken.None);
        if (cleanupTarget is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cleanupTarget.SocketPath)!);
            await File.WriteAllTextAsync(cleanupTarget.SocketPath, "stale endpoint", CancellationToken.None);
        }

        var formatException = await Assert.ThrowsAsync<SupervisorManifestFormatException>(
            () => store.ReadOrNullAsync(scope.FullPath, CancellationToken.None).AsTask());
        using var runtimeOwnership = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(scope.FullPath),
            AsyncTestTimeout,
            CancellationToken.None);
        runtimeOwnership.Dispose();

        var cleanupStatus = await store.CleanupObservedRuntimeIfMalformedArtifactMatchesAsync(
            scope.FullPath,
            formatException.ArtifactIdentity,
            cleanupTarget,
            AsyncTestTimeout,
            CancellationToken.None);

        Assert.Equal(SupervisorManifestCleanupStatus.Removed, cleanupStatus);
        Assert.False(File.Exists(manifestPath));
        if (cleanupTarget is not null)
        {
            Assert.False(File.Exists(cleanupTarget.SocketPath));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CleanupObservedMalformedRuntime_WhenCanceledWhileWaitingForOwnership_PreservesArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "canceled-malformed-cleanup");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpointResolver = new SupervisorEndpointResolver();
        var cleanupTarget = endpointResolver.ResolveUnixSocketCleanupTargetOrNull(scope.FullPath);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        const string malformedJson = "{ malformed json";
        await File.WriteAllTextAsync(manifestPath, malformedJson, CancellationToken.None);
        var formatException = await Assert.ThrowsAsync<SupervisorManifestFormatException>(
            () => store.ReadOrNullAsync(scope.FullPath, CancellationToken.None).AsTask());
        using var runtimeOwnership = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(scope.FullPath),
            AsyncTestTimeout,
            CancellationToken.None);
        using var cancellationTokenSource = new CancellationTokenSource();

        var cleanupTask = store.CleanupObservedRuntimeIfMalformedArtifactMatchesAsync(
                scope.FullPath,
                formatException.ArtifactIdentity,
                cleanupTarget,
                AsyncTestTimeout,
                cancellationTokenSource.Token)
            .AsTask();
        Assert.False(cleanupTask.IsCompleted);
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cleanupTask);
        Assert.Equal(malformedJson, await File.ReadAllTextAsync(manifestPath, CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EndpointPublicationLease_BlocksObservedOldGenerationCleanupUntilSuccessorManifestIsPublished ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-manifest-store", "endpoint-publication-race");
        var store = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var endpointResolver = new SupervisorEndpointResolver();
        var endpoint = endpointResolver.ResolveRuntimeEndpoint(
            scope.FullPath,
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1));
        var cleanupTarget = endpointResolver.ResolveUnixSocketCleanupTargetOrNull(scope.FullPath);
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

        var cleanupTask = store.CleanupObservedRuntimeIfManifestMatchesAsync(
                scope.FullPath,
                firstManifest,
                cleanupTarget,
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
        var serializedManifestBytes = await File.ReadAllBytesAsync(manifestPath, CancellationToken.None);
        var replacedManifestBytes = CombineBytes(
            Encoding.UTF8.GetPreamble(),
            serializedManifestBytes);
        await File.WriteAllBytesAsync(manifestPath, replacedManifestBytes, CancellationToken.None);
        var externalReadBytes = replacedManifestBytes.ToArray();
        var readCount = 0;
        var writeCount = 0;
        var store = new SupervisorManifestStore(
            TimeProvider.System,
            (path, cancellationToken) => Interlocked.Increment(ref readCount) == 1
                ? ValueTask.FromResult<ReadOnlyMemory<byte>?>(externalReadBytes)
                : FileUtilities.ReadAllBytesOrNullAsync(path, cancellationToken),
            async (path, contents, cancellationToken) =>
            {
                await FileUtilities.WriteAllBytesAtomicallyAsync(path, contents, cancellationToken);
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
        externalReadBytes.AsSpan().Fill(0x00);

        var exception = await Assert.ThrowsAsync<IOException>(
            async () => await publicationLease.PublishAsync(successorManifest, CancellationToken.None));

        Assert.Contains("after atomic replacement", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, writeCount);
        Assert.Equal(
            replacedManifestBytes,
            await File.ReadAllBytesAsync(manifestPath, CancellationToken.None));
        Assert.Equal(
            replacedManifest,
            await store.ReadOrNullAsync(scope.FullPath, CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EndpointPublicationLease_WhenRollbackOnlyMatchesDecodedText_ReportsExactRestorationFailure ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "supervisor-manifest-store",
            "publication-byte-mismatch");
        var replacedManifest = CreateManifest();
        var successorManifest = CreateManifest(
            sessionTokenDiscriminator: 2,
            issuedAtUtc: replacedManifest.IssuedAtUtc.AddSeconds(1));
        var replacedWithoutBom = Encoding.UTF8.GetBytes(
            SupervisorManifestStoreTestSupport.Serialize(replacedManifest));
        var replacedWithBom = CombineBytes(Encoding.UTF8.GetPreamble(), replacedWithoutBom);
        var currentBytes = replacedWithBom.ToArray();
        var writeCount = 0;
        var store = new SupervisorManifestStore(
            TimeProvider.System,
            (_, _) => ValueTask.FromResult<ReadOnlyMemory<byte>?>(currentBytes.ToArray()),
            (_, contents, _) =>
            {
                if (Interlocked.Increment(ref writeCount) == 1)
                {
                    currentBytes = contents.ToArray();
                    throw new IOException("Injected successor publication failure.");
                }

                currentBytes = replacedWithoutBom.ToArray();
                throw new IOException("Injected byte-inexact rollback failure.");
            },
            static _ => { });
        using var publicationLease = await store.AcquireEndpointPublicationLeaseAsync(
            scope.FullPath,
            SupervisorConstants.ManifestMutationLockTimeout,
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await publicationLease.PublishAsync(successorManifest, CancellationToken.None));

        Assert.Contains("could not be restored", exception.Message, StringComparison.Ordinal);
        var failures = Assert.IsType<AggregateException>(exception.InnerException);
        Assert.Collection(
            failures.InnerExceptions,
            publicationFailure => Assert.Contains("publication failure", publicationFailure.Message, StringComparison.Ordinal),
            rollbackFailure =>
            {
                var restorationFailure = Assert.IsType<InvalidOperationException>(rollbackFailure);
                Assert.Contains("did not restore", restorationFailure.Message, StringComparison.Ordinal);
                Assert.Contains("rollback failure", restorationFailure.InnerException!.Message, StringComparison.Ordinal);
            });
        Assert.Equal(replacedWithoutBom, currentBytes);
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

    private static byte[] CombineBytes (
        ReadOnlySpan<byte> prefix,
        ReadOnlySpan<byte> contents)
    {
        var result = new byte[prefix.Length + contents.Length];
        prefix.CopyTo(result);
        contents.CopyTo(result.AsSpan(prefix.Length));
        return result;
    }
}

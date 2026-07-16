using System.Diagnostics;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Composition.Common;
using MackySoft.Ucli.Hosting.Supervisor;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class InternalSupervisorExecutionRunnerTests
{
    private static readonly TimeSpan AsyncTestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public void BuildServiceProvider_ResolvesSupervisorHostThroughSharedCompositionRoot ()
    {
        using var serviceProvider = InternalSupervisorExecutionRunner.BuildServiceProvider();

        var supervisorHost = serviceProvider.GetRequiredService<SupervisorHost>();

        Assert.NotNull(supervisorHost);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenAnotherHostOwnsRuntime_DoesNotReplacePublishedManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-host", "duplicate-runtime-owner");
        using var firstServiceProvider = InternalSupervisorExecutionRunner.BuildServiceProvider();
        using var secondServiceProvider = InternalSupervisorExecutionRunner.BuildServiceProvider();
        var firstHost = firstServiceProvider.GetRequiredService<SupervisorHost>();
        var secondHost = secondServiceProvider.GetRequiredService<SupervisorHost>();
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        using var firstHostCancellation = new CancellationTokenSource();
        var firstHostTask = firstHost.RunAsync(scope.FullPath, firstHostCancellation.Token);

        try
        {
            await WaitForFileExistsAsync(manifestPath, AsyncTestTimeout);
            var firstManifestJson = await File.ReadAllTextAsync(manifestPath, CancellationToken.None);

            var secondExitCode = await secondHost
                .RunAsync(scope.FullPath, CancellationToken.None)
                .WaitAsync(AsyncTestTimeout);

            Assert.Equal(1, secondExitCode);
            Assert.False(firstHostTask.IsCompleted);
            Assert.Equal(
                firstManifestJson,
                await File.ReadAllTextAsync(manifestPath, CancellationToken.None));
        }
        finally
        {
            firstHostCancellation.Cancel();
            _ = await firstHostTask.WaitAsync(AsyncTestTimeout);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenCanceled_StopsListenerAndCleansOwnedManifestWithoutReacquiringRuntimeOwnership ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-host", "ownership-release-order");
        var processManager = new RecordingSupervisorProcessManager();
        using var serviceProvider = BuildServiceProvider(TimeProvider.System, processManager, manifestStore: null);
        var host = serviceProvider.GetRequiredService<SupervisorHost>();
        var cleanupTarget = serviceProvider
            .GetRequiredService<SupervisorEndpointResolver>()
            .ResolveUnixSocketCleanupTargetOrNull(scope.FullPath);
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        var ownershipLockPath = UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(scope.FullPath);
        using var hostCancellation = new CancellationTokenSource();
        var hostTask = host.RunAsync(scope.FullPath, hostCancellation.Token);

        await WaitForFileExistsAsync(manifestPath, AsyncTestTimeout);
        if (cleanupTarget is not null)
        {
            Assert.True(File.Exists(cleanupTarget.SocketPath));
        }

        var ownershipWaitTask = FileExclusiveLock.AcquireAsync(
                ownershipLockPath,
                AsyncTestTimeout,
                CancellationToken.None)
            .AsTask();
        Assert.False(ownershipWaitTask.IsCompleted);

        hostCancellation.Cancel();
        var exitCode = await hostTask.WaitAsync(AsyncTestTimeout);
        using var successorOwnership = await ownershipWaitTask.WaitAsync(AsyncTestTimeout);

        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(manifestPath));
        if (cleanupTarget is not null)
        {
            Assert.False(File.Exists(cleanupTarget.SocketPath));
        }

        Assert.Empty(processManager.ReleaseInvocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenIdleGenerationIsLast_ReleasesWorktreeRegistration ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-host", "idle-registration-release");
        var timeProvider = new ManualTimeProvider();
        var processManager = new RecordingSupervisorProcessManager
        {
            ReleaseHandler = (storageRoot, _) =>
            {
                var bootstrapLockPath = UcliStoragePathResolver.ResolveSupervisorBootstrapLockPath(storageRoot);
                Assert.Throws<IOException>(() =>
                {
                    using var competingLock = new FileStream(
                        bootstrapLockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                });
                return ValueTask.FromResult<ExecutionError?>(null);
            },
        };
        using var serviceProvider = BuildServiceProvider(timeProvider, processManager, manifestStore: null);
        var host = serviceProvider.GetRequiredService<SupervisorHost>();
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        var hostTask = host.RunAsync(scope.FullPath, CancellationToken.None);

        await WaitForFileExistsAsync(manifestPath, AsyncTestTimeout);
        await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromSeconds(1)).WaitAsync(AsyncTestTimeout);
        timeProvider.Advance(SupervisorConstants.IdleShutdownDelay);

        var exitCode = await hostTask.WaitAsync(AsyncTestTimeout);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(manifestPath));
        var releaseInvocation = Assert.Single(processManager.ReleaseInvocations);
        Assert.Equal(UcliStoragePathResolver.NormalizeStorageRootPath(scope.FullPath), releaseInvocation.StorageRoot);
        Assert.Equal(CancellationToken.None, releaseInvocation.CancellationToken);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenManifestGenerationChangesBeforeIdleCleanup_PreservesRegistration ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-host", "idle-generation-mismatch");
        var timeProvider = new ManualTimeProvider();
        var processManager = new RecordingSupervisorProcessManager();
        using var serviceProvider = BuildServiceProvider(timeProvider, processManager, manifestStore: null);
        var host = serviceProvider.GetRequiredService<SupervisorHost>();
        var manifestStore = serviceProvider.GetRequiredService<SupervisorManifestStore>();
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        var hostTask = host.RunAsync(scope.FullPath, CancellationToken.None);

        await WaitForFileExistsAsync(manifestPath, AsyncTestTimeout);
        var publishedManifest = await manifestStore.ReadOrNullAsync(
                scope.FullPath,
                AsyncTestTimeout,
                CancellationToken.None)
            ?? throw new InvalidOperationException("Published supervisor manifest was not found.");
        var replacementManifest = new SupervisorInstanceManifest(
            publishedManifest.ProcessId,
            IpcSessionTokenTestFactory.CreateFromDiscriminator(99),
            publishedManifest.Endpoint,
            publishedManifest.IssuedAtUtc.AddTicks(1));
        await manifestStore.WriteAsync(scope.FullPath, replacementManifest, CancellationToken.None);
        await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromSeconds(1)).WaitAsync(AsyncTestTimeout);
        timeProvider.Advance(SupervisorConstants.IdleShutdownDelay);

        var exitCode = await hostTask.WaitAsync(AsyncTestTimeout);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            replacementManifest,
            await manifestStore.ReadOrNullAsync(scope.FullPath, AsyncTestTimeout, CancellationToken.None));
        Assert.Empty(processManager.ReleaseInvocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenSuccessorPublishesAfterIdleCleanup_DoesNotReleaseSuccessorRegistration ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-host", "successor-after-idle-cleanup");
        var timeProvider = new ManualTimeProvider();
        var processManager = new RecordingSupervisorProcessManager();
        using var firstServiceProvider = BuildServiceProvider(timeProvider, processManager, manifestStore: null);
        using var successorServiceProvider = BuildServiceProvider(timeProvider, processManager, manifestStore: null);
        var firstHost = firstServiceProvider.GetRequiredService<SupervisorHost>();
        var successorHost = successorServiceProvider.GetRequiredService<SupervisorHost>();
        var bootstrapLockProvider = firstServiceProvider.GetRequiredService<SupervisorBootstrapLockProvider>();
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        using var firstCancellation = new CancellationTokenSource();
        using var successorCancellation = new CancellationTokenSource();
        var firstHostTask = firstHost.RunAsync(scope.FullPath, firstCancellation.Token);
        Task<int>? successorHostTask = null;
        IAsyncDisposable? bootstrapLock = null;

        try
        {
            await WaitForFileExistsAsync(manifestPath, AsyncTestTimeout);
            bootstrapLock = await bootstrapLockProvider.AcquireAsync(
                scope.FullPath,
                AsyncTestTimeout,
                CancellationToken.None);
            await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromSeconds(1)).WaitAsync(AsyncTestTimeout);
            timeProvider.Advance(SupervisorConstants.IdleShutdownDelay);
            await WaitForFileMissingAsync(manifestPath, AsyncTestTimeout);
            Assert.False(firstHostTask.IsCompleted);

            successorHostTask = successorHost.RunAsync(scope.FullPath, successorCancellation.Token);
            await WaitForFileExistsAsync(manifestPath, AsyncTestTimeout);
            await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(50)).WaitAsync(AsyncTestTimeout);
            await bootstrapLock.DisposeAsync();
            bootstrapLock = null;
            timeProvider.Advance(TimeSpan.FromMilliseconds(50));

            Assert.Equal(0, await firstHostTask.WaitAsync(AsyncTestTimeout));
            Assert.Empty(processManager.ReleaseInvocations);
        }
        finally
        {
            if (bootstrapLock is not null)
            {
                await bootstrapLock.DisposeAsync();
            }

            firstCancellation.Cancel();
            successorCancellation.Cancel();
            timeProvider.Advance(TimeSpan.FromMilliseconds(50));
            _ = await firstHostTask.WaitAsync(AsyncTestTimeout);
            if (successorHostTask is not null)
            {
                _ = await successorHostTask.WaitAsync(AsyncTestTimeout);
            }
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenEndpointPublicationFails_ReleasesRuntimeOwnershipAndProcessRegistration ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-host", "publication-failure-release");
        var processManager = new RecordingSupervisorProcessManager
        {
            ReleaseHandler = static (_, _) => ValueTask.FromResult<ExecutionError?>(null),
        };
        var manifestStore = new SupervisorManifestStore(
            TimeProvider.System,
            readAllBytesOrNull: static (_, _) => ValueTask.FromResult<ReadOnlyMemory<byte>?>(null),
            writeAllBytesAtomically: static (_, _, _) => ValueTask.FromException(new IOException("Manifest publication failed.")),
            deleteIfExists: static _ => { });
        using var serviceProvider = BuildServiceProvider(TimeProvider.System, processManager, manifestStore);
        var host = serviceProvider.GetRequiredService<SupervisorHost>();
        var ownershipLockPath = UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(scope.FullPath);
        using var hostCancellation = new CancellationTokenSource();
        var hostTask = host.RunAsync(scope.FullPath, hostCancellation.Token);

        try
        {
            var exitCode = await hostTask.WaitAsync(AsyncTestTimeout);
            using var successorOwnership = await FileExclusiveLock.AcquireAsync(
                ownershipLockPath,
                AsyncTestTimeout,
                CancellationToken.None);

            Assert.Equal(1, exitCode);
            var releaseInvocation = Assert.Single(processManager.ReleaseInvocations);
            Assert.Equal(UcliStoragePathResolver.NormalizeStorageRootPath(scope.FullPath), releaseInvocation.StorageRoot);
        }
        finally
        {
            if (!hostTask.IsCompleted)
            {
                hostCancellation.Cancel();
                _ = await hostTask.WaitAsync(AsyncTestTimeout);
            }
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunAsync_WhenEndpointPreparationThrowsUnrelatedCancellation_ReturnsFailureAndReleasesRegistration ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-host", "unexpected-cancellation-release");
        var readCount = 0;
        var processManager = new RecordingSupervisorProcessManager
        {
            ReleaseHandler = static (_, _) => ValueTask.FromResult<ExecutionError?>(null),
        };
        var manifestStore = new SupervisorManifestStore(
            TimeProvider.System,
            readAllBytesOrNull: (_, _) => Interlocked.Increment(ref readCount) == 1
                ? ValueTask.FromException<ReadOnlyMemory<byte>?>(new OperationCanceledException("Unrelated cancellation."))
                : ValueTask.FromResult<ReadOnlyMemory<byte>?>(null),
            writeAllBytesAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        using var serviceProvider = BuildServiceProvider(TimeProvider.System, processManager, manifestStore);
        var host = serviceProvider.GetRequiredService<SupervisorHost>();

        var exitCode = await host.RunAsync(scope.FullPath, CancellationToken.None).WaitAsync(AsyncTestTimeout);

        Assert.Equal(1, exitCode);
        Assert.Single(processManager.ReleaseInvocations);
    }

    private static ServiceProvider BuildServiceProvider (
        TimeProvider timeProvider,
        ISupervisorProcessManager processManager,
        SupervisorManifestStore? manifestStore)
    {
        var services = new ServiceCollection();
        services.AddUcliServices();
        services.AddSingleton(timeProvider);
        services.AddSingleton(processManager);
        if (manifestStore is not null)
        {
            services.AddSingleton(manifestStore);
        }

        return services.BuildServiceProvider();
    }

    private static async Task WaitForFileExistsAsync (
        string path,
        TimeSpan timeout)
    {
        var waitElapsedTime = Stopwatch.StartNew();
        while (waitElapsedTime.Elapsed < timeout)
        {
            if (File.Exists(path))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5));
        }

        Assert.Fail($"File was not created within {timeout}: {path}");
    }

    private static async Task WaitForFileMissingAsync (
        string path,
        TimeSpan timeout)
    {
        var waitElapsedTime = Stopwatch.StartNew();
        while (waitElapsedTime.Elapsed < timeout)
        {
            if (!File.Exists(path))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5));
        }

        Assert.Fail($"File was not removed within {timeout}: {path}");
    }
}

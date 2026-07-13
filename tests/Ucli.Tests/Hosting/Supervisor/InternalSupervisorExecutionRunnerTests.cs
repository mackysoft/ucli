using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Supervisor;
using MackySoft.Ucli.Infrastructure.Storage;
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
        using var serviceProvider = InternalSupervisorExecutionRunner.BuildServiceProvider();
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
    }

    private static async Task WaitForFileExistsAsync (
        string path,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5));
        }

        Assert.Fail($"File was not created within {timeout}: {path}");
    }
}

namespace MackySoft.Ucli.Tests.Execution;

using MackySoft.Tests;
using Xunit.Sdk;

public sealed class FileSystemProjectLifecycleLockProviderTests
{
    private static readonly TimeSpan AcquireWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WhenLockAlreadyHeld_WaitsUntilReleased ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "wait-until-release");
        var timeProvider = new ManualTimeProvider();
        var provider = CreateProvider(scope, timeProvider);
        var unityProject = CreateContext(
            scope.CreateDirectory("UnityProject"),
            scope.CreateDirectory("Repo"),
            "fingerprint-lock");
        var firstHandle = await provider.Acquire(
            unityProject,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        var secondAcquireTask = provider.Acquire(
            unityProject,
            TimeSpan.FromSeconds(2),
            CancellationToken.None).AsTask();

        Assert.False(secondAcquireTask.IsCompleted);

        await firstHandle.DisposeAsync();
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        var secondHandle = await TestAwaiter.WaitAsync(secondAcquireTask, "File system lifecycle lock reacquire", AcquireWaitTimeout);
        await secondHandle.DisposeAsync();
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WhenCanceledWhileWaiting_ThrowsOperationCanceledException ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "cancel-while-waiting");
        var timeProvider = new ManualTimeProvider();
        var provider = CreateProvider(scope, timeProvider);
        var unityProject = CreateContext(
            scope.CreateDirectory("UnityProject"),
            scope.CreateDirectory("Repo"),
            "fingerprint-lock");
        var firstHandle = await provider.Acquire(
            unityProject,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        using var waitingCts = new CancellationTokenSource();

        var waitingTask = provider.Acquire(
                unityProject,
                TimeSpan.FromSeconds(5),
                waitingCts.Token)
            .AsTask();
        Assert.False(waitingTask.IsCompleted);
        waitingCts.Cancel();
        var exception = await Record.ExceptionAsync(async () =>
        {
            await TestAwaiter.WaitAsync(waitingTask, "Canceled file system lifecycle lock acquire", AcquireWaitTimeout);
        });

        await firstHandle.DisposeAsync();

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WhenTimeoutWhileWaiting_ThrowsTimeoutException ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "timeout-while-waiting");
        var timeProvider = new ManualTimeProvider();
        var provider = CreateProvider(scope, timeProvider);
        var unityProject = CreateContext(
            scope.CreateDirectory("UnityProject"),
            scope.CreateDirectory("Repo"),
            "fingerprint-lock");
        var firstHandle = await provider.Acquire(
            unityProject,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        var waitingTask = provider.Acquire(
                unityProject,
                TimeSpan.FromMilliseconds(150),
                CancellationToken.None)
            .AsTask();
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));
        var exception = await Record.ExceptionAsync(async () =>
        {
            await TestAwaiter.WaitAsync(waitingTask, "Timed out file system lifecycle lock acquire", AcquireWaitTimeout);
        });

        await firstHandle.DisposeAsync();

        Assert.IsType<TimeoutException>(exception);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WithDifferentStorageRootsAndFingerprintsForSamePhysicalProject_WaitsUntilReleased ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "same-physical-project");
        var timeProvider = new ManualTimeProvider();
        var provider = CreateProvider(scope, timeProvider);
        var unityProjectRoot = scope.CreateDirectory("UnityProject");
        var firstContext = CreateContext(
            unityProjectRoot,
            scope.CreateDirectory("RepoA"),
            "fingerprint-a");
        var secondContext = CreateContext(
            unityProjectRoot,
            scope.CreateDirectory("RepoB"),
            "fingerprint-b");
        var firstHandle = await provider.Acquire(
            firstContext,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        var secondAcquireTask = provider.Acquire(
            secondContext,
            TimeSpan.FromSeconds(2),
            CancellationToken.None).AsTask();

        Assert.False(secondAcquireTask.IsCompleted);

        await firstHandle.DisposeAsync();
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        var secondHandle = await TestAwaiter.WaitAsync(secondAcquireTask, "Cross-storage lifecycle lock reacquire", AcquireWaitTimeout);
        await secondHandle.DisposeAsync();
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WithDifferentPhysicalProjectRoots_DoesNotWaitForHeldLock ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "different-physical-project");
        var provider = CreateProvider(scope, new ManualTimeProvider());
        var repositoryRoot = scope.CreateDirectory("Repo");
        var firstContext = CreateContext(
            scope.CreateDirectory("UnityProjectA"),
            repositoryRoot,
            "fingerprint-a");
        var secondContext = CreateContext(
            scope.CreateDirectory("UnityProjectB"),
            repositoryRoot,
            "fingerprint-b");
        var firstHandle = await provider.Acquire(
            firstContext,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        var secondHandle = await provider.Acquire(
            secondContext,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        await secondHandle.DisposeAsync();
        await firstHandle.DisposeAsync();
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WithSymlinkProjectRoot_UsesTargetPhysicalProjectLock ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "symlink-project");
        var timeProvider = new ManualTimeProvider();
        var provider = CreateProvider(scope, timeProvider);
        var targetProjectRoot = scope.CreateDirectory(Path.Combine("target", "UnityProject"));
        var symlinkProjectRoot = Path.Combine(scope.FullPath, "linked-project");
        if (!TryCreateDirectorySymbolicLink(symlinkProjectRoot, targetProjectRoot))
        {
            throw SkipException.ForSkip("Skipping because symbolic link creation is not available in this environment.");
        }

        var firstHandle = await provider.Acquire(
            CreateContext(targetProjectRoot, scope.CreateDirectory("RepoA"), "fingerprint-a"),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        var secondAcquireTask = provider.Acquire(
            CreateContext(symlinkProjectRoot, scope.CreateDirectory("RepoB"), "fingerprint-b"),
            TimeSpan.FromSeconds(2),
            CancellationToken.None).AsTask();

        Assert.False(secondAcquireTask.IsCompleted);

        await firstHandle.DisposeAsync();
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        var secondHandle = await TestAwaiter.WaitAsync(secondAcquireTask, "Symlink lifecycle lock reacquire", AcquireWaitTimeout);
        await secondHandle.DisposeAsync();
    }

    private static FileSystemProjectLifecycleLockProvider CreateProvider (
        TestDirectoryScope scope,
        TimeProvider timeProvider)
    {
        return new FileSystemProjectLifecycleLockProvider(
            timeProvider,
            scope.CreateDirectory("locks"));
    }

    private static ResolvedUnityProjectContext CreateContext (
        string unityProjectRoot,
        string repositoryRoot,
        string projectFingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: unityProjectRoot,
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: projectFingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static bool TryCreateDirectorySymbolicLink (
        string symbolicLinkPath,
        string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(symbolicLinkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }
}

namespace MackySoft.Ucli.Tests.Execution;

using System.Runtime.Versioning;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Tests.Helpers;

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
        var lockRequest = CreateRequest(scope.CreateDirectory("UnityProject"));
        var firstHandle = await provider.AcquireAsync(
            lockRequest,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        var secondAcquireTask = provider.AcquireAsync(
            lockRequest,
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
        var lockRequest = CreateRequest(scope.CreateDirectory("UnityProject"));
        var firstHandle = await provider.AcquireAsync(
            lockRequest,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        using var waitingCts = new CancellationTokenSource();

        var waitingTask = provider.AcquireAsync(
                lockRequest,
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
        var lockRequest = CreateRequest(scope.CreateDirectory("UnityProject"));
        var firstHandle = await provider.AcquireAsync(
            lockRequest,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        var waitingTask = provider.AcquireAsync(
                lockRequest,
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
    public async Task Acquire_WithSamePhysicalProjectRootAcrossProviders_WaitsUntilReleased ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "same-physical-project");
        var timeProvider = new ManualTimeProvider();
        var lockStorageRoot = scope.CreateDirectory("locks");
        var firstProvider = CreateProvider(lockStorageRoot, timeProvider);
        var secondProvider = CreateProvider(lockStorageRoot, timeProvider);
        var unityProjectRoot = scope.CreateDirectory("UnityProject");
        var firstRequest = CreateRequest(unityProjectRoot);
        var secondRequest = CreateRequest(unityProjectRoot);
        var firstHandle = await firstProvider.AcquireAsync(
            firstRequest,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        var secondAcquireTask = secondProvider.AcquireAsync(
            secondRequest,
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
        var firstRequest = CreateRequest(scope.CreateDirectory("UnityProjectA"));
        var secondRequest = CreateRequest(scope.CreateDirectory("UnityProjectB"));
        var firstHandle = await provider.AcquireAsync(
            firstRequest,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        var secondHandle = await provider.AcquireAsync(
            secondRequest,
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
        var lockStorageRoot = scope.CreateDirectory("locks");
        var firstProvider = CreateProvider(lockStorageRoot, timeProvider);
        var secondProvider = CreateProvider(lockStorageRoot, timeProvider);
        var targetProjectRoot = scope.CreateDirectory(Path.Combine("target", "UnityProject"));
        var symlinkProjectRoot = Path.Combine(scope.FullPath, "linked-project");
        if (!TestSymbolicLinks.TryCreateDirectory(symlinkProjectRoot, targetProjectRoot))
        {
            return;
        }

        var firstHandle = await firstProvider.AcquireAsync(
            CreateRequest(targetProjectRoot),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        var secondAcquireTask = secondProvider.AcquireAsync(
            CreateRequest(symlinkProjectRoot),
            TimeSpan.FromSeconds(2),
            CancellationToken.None).AsTask();

        Assert.False(secondAcquireTask.IsCompleted);

        await firstHandle.DisposeAsync();
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        var secondHandle = await TestAwaiter.WaitAsync(secondAcquireTask, "Symlink lifecycle lock reacquire", AcquireWaitTimeout);
        await secondHandle.DisposeAsync();
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Acquire_WithCaseVariantProjectRootOnCaseInsensitiveFileSystem_UsesSamePhysicalProjectLock ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "case-variant-project");
        var timeProvider = new ManualTimeProvider();
        var lockStorageRoot = scope.CreateDirectory("locks");
        var firstProvider = CreateProvider(lockStorageRoot, timeProvider);
        var secondProvider = CreateProvider(lockStorageRoot, timeProvider);
        var projectRoot = scope.CreateDirectory("UnityProject");
        var caseVariantProjectRoot = CreateCaseVariantPath(projectRoot);
        if (string.Equals(caseVariantProjectRoot, projectRoot, StringComparison.Ordinal)
            || !Directory.Exists(caseVariantProjectRoot))
        {
            return;
        }

        var firstHandle = await firstProvider.AcquireAsync(
            CreateRequest(projectRoot),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        var secondAcquireTask = secondProvider.AcquireAsync(
            CreateRequest(caseVariantProjectRoot),
            TimeSpan.FromSeconds(2),
            CancellationToken.None).AsTask();

        Assert.False(secondAcquireTask.IsCompleted);

        await firstHandle.DisposeAsync();
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        var secondHandle = await TestAwaiter.WaitAsync(secondAcquireTask, "Case-variant lifecycle lock reacquire", AcquireWaitTimeout);
        await secondHandle.DisposeAsync();
    }

    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public async Task Acquire_OnUnix_CreatesLockStorageDirectoryChainWithOwnerOnlyAccess ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-lock", "owner-only");
        var timeProvider = new ManualTimeProvider();
        var lockStorageRoot = Path.Combine(scope.FullPath, "locks", "unity-projects");
        var provider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var handle = await provider.AcquireAsync(
            CreateRequest(scope.CreateDirectory("UnityProject")),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        await handle.DisposeAsync();

        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(lockStorageRoot);
        var lockKeyDirectoryPath = Assert.Single(Directory.EnumerateDirectories(lockStorageRoot));
        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(lockKeyDirectoryPath);
    }

    private static FileSystemProjectLifecycleLockProvider CreateProvider (
        TestDirectoryScope scope,
        TimeProvider timeProvider)
    {
        return CreateProvider(scope.CreateDirectory("locks"), timeProvider);
    }

    private static FileSystemProjectLifecycleLockProvider CreateProvider (
        string lockStorageRoot,
        TimeProvider timeProvider)
    {
        return new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
    }

    private static ProjectLifecycleLockRequest CreateRequest (string unityProjectRoot)
    {
        return new ProjectLifecycleLockRequest(unityProjectRoot);
    }

    private static string CreateCaseVariantPath (string path)
    {
        var characters = path.ToCharArray();
        for (var i = 0; i < characters.Length; i++)
        {
            var character = characters[i];
            if (char.IsUpper(character))
            {
                characters[i] = char.ToLowerInvariant(character);
            }
            else if (char.IsLower(character))
            {
                characters[i] = char.ToUpperInvariant(character);
            }
        }

        return new string(characters);
    }
}

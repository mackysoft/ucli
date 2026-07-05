namespace MackySoft.Ucli.Tests.Execution;

using System.Runtime.Versioning;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Tests.Helpers;

public sealed class FileSystemProjectLifecycleLockProviderTests
{
    private static readonly TimeSpan AcquireWaitTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan InitialAcquireTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan ContendedAcquireTimeout = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(50);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WhenLockAlreadyHeld_WaitsUntilReleased ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "wait-until-release");
        var timeProvider = new ManualTimeProvider();
        var provider = CreateProvider(scope, timeProvider);
        var lockRequest = new ProjectLifecycleLockRequest(scope.CreateDirectory("UnityProject"));
        await AssertSecondAcquireWaitsForReleaseAsync(
            provider,
            provider,
            timeProvider,
            lockRequest,
            lockRequest,
            "File system lifecycle lock reacquire");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WhenCanceledWhileWaiting_ThrowsOperationCanceledException ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "cancel-while-waiting");
        var timeProvider = new ManualTimeProvider();
        var provider = CreateProvider(scope, timeProvider);
        var lockRequest = new ProjectLifecycleLockRequest(scope.CreateDirectory("UnityProject"));
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
    [Trait("Size", "Medium")]
    public async Task Acquire_WhenTimeoutWhileWaiting_ThrowsTimeoutException ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "timeout-while-waiting");
        var timeProvider = new ManualTimeProvider();
        var provider = CreateProvider(scope, timeProvider);
        var lockRequest = new ProjectLifecycleLockRequest(scope.CreateDirectory("UnityProject"));
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
    [Trait("Size", "Medium")]
    public async Task Acquire_WithSamePhysicalProjectRootAcrossProviders_WaitsUntilReleased ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "same-physical-project");
        var timeProvider = new ManualTimeProvider();
        var lockStorageRoot = scope.CreateDirectory("locks");
        var firstProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var secondProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var unityProjectRoot = scope.CreateDirectory("UnityProject");
        var firstRequest = new ProjectLifecycleLockRequest(unityProjectRoot);
        var secondRequest = new ProjectLifecycleLockRequest(unityProjectRoot);
        await AssertSecondAcquireWaitsForReleaseAsync(
            firstProvider,
            secondProvider,
            timeProvider,
            firstRequest,
            secondRequest,
            "Cross-storage lifecycle lock reacquire");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WithDifferentPhysicalProjectRoots_DoesNotWaitForHeldLock ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "different-physical-project");
        var provider = CreateProvider(scope, new ManualTimeProvider());
        var firstRequest = new ProjectLifecycleLockRequest(scope.CreateDirectory("UnityProjectA"));
        var secondRequest = new ProjectLifecycleLockRequest(scope.CreateDirectory("UnityProjectB"));
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
    [Trait("Size", "Medium")]
    public async Task Acquire_WithSymlinkProjectRoot_UsesTargetPhysicalProjectLock ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "symlink-project");
        var timeProvider = new ManualTimeProvider();
        var lockStorageRoot = scope.CreateDirectory("locks");
        var firstProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var secondProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var targetProjectRoot = scope.CreateDirectory(Path.Combine("target", "UnityProject"));
        var symlinkProjectRoot = Path.Combine(scope.FullPath, "linked-project");
        if (!TestSymbolicLinks.TryCreateDirectory(symlinkProjectRoot, targetProjectRoot))
        {
            return;
        }

        await AssertSecondAcquireWaitsForReleaseAsync(
            firstProvider,
            secondProvider,
            timeProvider,
            new ProjectLifecycleLockRequest(targetProjectRoot),
            new ProjectLifecycleLockRequest(symlinkProjectRoot),
            "Symlink lifecycle lock reacquire");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WithCaseVariantProjectRootOnCaseInsensitiveFileSystem_UsesSamePhysicalProjectLock ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "case-variant-project");
        var timeProvider = new ManualTimeProvider();
        var lockStorageRoot = scope.CreateDirectory("locks");
        var firstProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var secondProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var projectRoot = scope.CreateDirectory("UnityProject");
        var caseVariantProjectRoot = CreateLeafCaseVariantPath(projectRoot);
        if (string.Equals(caseVariantProjectRoot, projectRoot, StringComparison.Ordinal)
            || !Directory.Exists(caseVariantProjectRoot))
        {
            return;
        }

        await AssertSecondAcquireWaitsForReleaseAsync(
            firstProvider,
            secondProvider,
            timeProvider,
            new ProjectLifecycleLockRequest(projectRoot),
            new ProjectLifecycleLockRequest(caseVariantProjectRoot),
            "Case-variant lifecycle lock reacquire");
    }

    [Fact]
    [Trait("Size", "Medium")]
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
            new ProjectLifecycleLockRequest(scope.CreateDirectory("UnityProject")),
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
        return new FileSystemProjectLifecycleLockProvider(timeProvider, scope.CreateDirectory("locks"));
    }

    private static async Task AssertSecondAcquireWaitsForReleaseAsync (
        FileSystemProjectLifecycleLockProvider firstProvider,
        FileSystemProjectLifecycleLockProvider secondProvider,
        ManualTimeProvider timeProvider,
        ProjectLifecycleLockRequest firstRequest,
        ProjectLifecycleLockRequest secondRequest,
        string waitDescription)
    {
        IAsyncDisposable? firstHandle = await firstProvider.AcquireAsync(
            firstRequest,
            InitialAcquireTimeout,
            CancellationToken.None);
        try
        {
            var secondAcquireTask = secondProvider.AcquireAsync(
                secondRequest,
                ContendedAcquireTimeout,
                CancellationToken.None).AsTask();

            Assert.False(secondAcquireTask.IsCompleted);

            await firstHandle.DisposeAsync();
            firstHandle = null;
            timeProvider.Advance(LockRetryDelay);
            var secondHandle = await TestAwaiter.WaitAsync(secondAcquireTask, waitDescription, AcquireWaitTimeout);
            await secondHandle.DisposeAsync();
        }
        finally
        {
            if (firstHandle != null)
            {
                await firstHandle.DisposeAsync();
            }
        }
    }

    private static string CreateLeafCaseVariantPath (string path)
    {
        var parentPath = Path.GetDirectoryName(path);
        Assert.False(string.IsNullOrWhiteSpace(parentPath));
        var leafName = Path.GetFileName(path);
        Assert.False(string.IsNullOrWhiteSpace(leafName));
        var characters = leafName.ToCharArray();
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

        return Path.Combine(parentPath, new string(characters));
    }
}

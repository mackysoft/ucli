namespace MackySoft.Ucli.Tests.Execution;

using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using MackySoft.FileSystem;
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
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var provider = CreateProvider(scope, timeProvider);
        var lockRequest = new ProjectLifecycleLockRequest(AbsolutePath.Parse(scope.CreateDirectory("UnityProject")));
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
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var provider = CreateProvider(scope, timeProvider);
        var lockRequest = new ProjectLifecycleLockRequest(AbsolutePath.Parse(scope.CreateDirectory("UnityProject")));
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
            await waitingTask.WaitAsync(AcquireWaitTimeout);
        });

        await firstHandle.DisposeAsync();

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WhenTimeoutWhileWaiting_ThrowsTimeoutException ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "timeout-while-waiting");
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var provider = CreateProvider(scope, timeProvider);
        var lockRequest = new ProjectLifecycleLockRequest(AbsolutePath.Parse(scope.CreateDirectory("UnityProject")));
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
            await waitingTask.WaitAsync(AcquireWaitTimeout);
        });

        await firstHandle.DisposeAsync();

        Assert.IsType<TimeoutException>(exception);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WithSamePhysicalProjectRootAcrossProviders_WaitsUntilReleased ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "same-physical-project");
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var lockStorageRoot = AbsolutePath.Parse(scope.CreateDirectory("locks"));
        var firstProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var secondProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var unityProjectRoot = scope.CreateDirectory("UnityProject");
        var firstRequest = new ProjectLifecycleLockRequest(AbsolutePath.Parse(unityProjectRoot));
        var secondRequest = new ProjectLifecycleLockRequest(AbsolutePath.Parse(unityProjectRoot));
        await AssertSecondAcquireWaitsForReleaseAsync(
            firstProvider,
            secondProvider,
            timeProvider,
            firstRequest,
            secondRequest,
            "Cross-storage lifecycle lock reacquire");
    }

    [Theory]
    [InlineData("workspace. ")]
    [InlineData("workspace ")]
    [SupportedOSPlatform("windows")]
    public void CreateRequest_OnWindows_WithEndpointNormalizedIntermediateComponent_IsRejectedAtPathFactory (
        string invalidIntermediateComponent)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-lock", "invalid-intermediate-component");
        var rawProjectRoot = Path.Combine(
            scope.FullPath,
            invalidIntermediateComponent,
            "UnityProject");

        var success = AbsolutePath.TryParse(
            rawProjectRoot,
            out var projectRoot,
            out var failure);

        Assert.False(success);
        Assert.Null(projectRoot);
        Assert.Equal(PathValidationFailureKind.InvalidPathFormat, failure.Kind);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WithNestedOrdinaryProjectRoot_PreservesPathWhileResolvingParents ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "nested-ordinary-project");
        var lockStorageRoot = AbsolutePath.Parse(scope.CreateDirectory("locks"));
        var projectRoot = AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("src", "Ucli"));
        var expectedLockKey = CreateExpectedLockKey(projectRoot);
        var expectedLockFilePath = Path.Combine(
            lockStorageRoot.Value,
            expectedLockKey,
            "lifecycle.lock");
        var provider = new FileSystemProjectLifecycleLockProvider(
            new FakeTimeProvider(DateTimeOffset.UnixEpoch),
            lockStorageRoot);

        var handle = await provider.AcquireAsync(
            new ProjectLifecycleLockRequest(projectRoot),
            InitialAcquireTimeout,
            CancellationToken.None);

        Assert.True(File.Exists(expectedLockFilePath));

        await handle.DisposeAsync();
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WithDifferentPhysicalProjectRoots_DoesNotWaitForHeldLock ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "different-physical-project");
        var provider = CreateProvider(scope, new FakeTimeProvider(DateTimeOffset.UnixEpoch));
        var firstRequest = new ProjectLifecycleLockRequest(AbsolutePath.Parse(scope.CreateDirectory("UnityProjectA")));
        var secondRequest = new ProjectLifecycleLockRequest(AbsolutePath.Parse(scope.CreateDirectory("UnityProjectB")));
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
    public async Task Acquire_WithExactAndSymlinkProjectRoots_UsesOnePhysicalProjectLock ()
    {
        using var scope = CreatePhysicalResolutionScope("symlink-project");
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var lockStorageRoot = AbsolutePath.Parse(scope.CreateDirectory("locks"));
        var firstProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var secondProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var targetProjectRoot = scope.CreateDirectory(Path.Combine("target", "UnityProject"));
        var symlinkProjectRoot = scope.CreateDirectorySymbolicLink("linked-project", targetProjectRoot);
        await AssertSecondAcquireWaitsForReleaseAsync(
            firstProvider,
            secondProvider,
            timeProvider,
            new ProjectLifecycleLockRequest(AbsolutePath.Parse(targetProjectRoot)),
            new ProjectLifecycleLockRequest(AbsolutePath.Parse(symlinkProjectRoot)),
            "Symlink lifecycle lock reacquire");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WithAncestorSymlinkProjectRoot_UsesTargetPhysicalProjectLock ()
    {
        using var scope = CreatePhysicalResolutionScope("ancestor-symlink-project");
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var lockStorageRoot = AbsolutePath.Parse(scope.CreateDirectory("locks"));
        var firstProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var secondProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var targetAncestorPath = scope.CreateDirectory("target");
        var targetProjectRoot = Directory.CreateDirectory(Path.Combine(targetAncestorPath, "UnityProject")).FullName;
        var linkedAncestorPath = scope.CreateDirectorySymbolicLink("linked-ancestor", targetAncestorPath);
        var linkedProjectRoot = Path.Combine(linkedAncestorPath, "UnityProject");
        await AssertSecondAcquireWaitsForReleaseAsync(
            firstProvider,
            secondProvider,
            timeProvider,
            new ProjectLifecycleLockRequest(AbsolutePath.Parse(targetProjectRoot)),
            new ProjectLifecycleLockRequest(AbsolutePath.Parse(linkedProjectRoot)),
            "Ancestor-symlink lifecycle lock reacquire");
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("windows")]
    public async Task Acquire_OnWindows_WithJunctionProjectRoot_UsesTargetPhysicalProjectLock ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = CreatePhysicalResolutionScope("junction-project");
        var targetProjectRoot = scope.CreateDirectory(Path.Combine("target", "UnityProject"));
        var junctionProjectRoot = Path.Combine(scope.FullPath, "junction-project");
        var junctionCreation = await CreateWindowsJunctionAsync(junctionProjectRoot, targetProjectRoot);
        if (!junctionCreation.Succeeded)
        {
            var cleanupException = Record.Exception(scope.Dispose);
            Assert.True(junctionCreation.Succeeded, junctionCreation.CreateFailureMessage(cleanupException));
        }

        scope.RegisterDirectoryLink(junctionProjectRoot);

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var lockStorageRoot = AbsolutePath.Parse(scope.CreateDirectory("locks"));
        var firstProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var secondProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        await AssertSecondAcquireWaitsForReleaseAsync(
            firstProvider,
            secondProvider,
            timeProvider,
            new ProjectLifecycleLockRequest(AbsolutePath.Parse(targetProjectRoot)),
            new ProjectLifecycleLockRequest(AbsolutePath.Parse(junctionProjectRoot)),
            "Junction lifecycle lock reacquire");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WithCaseVariantProjectRootOnCaseInsensitiveFileSystem_UsesSamePhysicalProjectLock ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "case-variant-project");
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var lockStorageRoot = AbsolutePath.Parse(scope.CreateDirectory("locks"));
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
            new ProjectLifecycleLockRequest(AbsolutePath.Parse(projectRoot)),
            new ProjectLifecycleLockRequest(AbsolutePath.Parse(caseVariantProjectRoot)),
            "Case-variant lifecycle lock reacquire");
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("windows")]
    public async Task Acquire_OnWindows_WithRootCaseVariant_UsesSamePhysicalProjectLock ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-lock", "root-case-variant-project");
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var lockStorageRoot = AbsolutePath.Parse(scope.CreateDirectory("locks"));
        var firstProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var secondProvider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var projectRoot = AbsolutePath.Parse(scope.CreateDirectory("UnityProject"));
        var rootCaseVariantProjectRoot = AbsolutePath.Parse(CreateRootCaseVariantPath(projectRoot.Value));

        Assert.Equal(projectRoot, rootCaseVariantProjectRoot);
        Assert.NotEqual(projectRoot.Value, rootCaseVariantProjectRoot.Value);

        await AssertSecondAcquireWaitsForReleaseAsync(
            firstProvider,
            secondProvider,
            timeProvider,
            new ProjectLifecycleLockRequest(projectRoot),
            new ProjectLifecycleLockRequest(rootCaseVariantProjectRoot),
            "Root-case-variant lifecycle lock reacquire");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WithMissingProjectRoot_ThrowsDirectoryNotFoundException ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-lock", "missing-project");
        var provider = CreateProvider(scope, new FakeTimeProvider(DateTimeOffset.UnixEpoch));
        var missingProjectRoot = AbsolutePath.Parse(Path.Combine(scope.FullPath, "missing", "UnityProject"));

        var exception = await Record.ExceptionAsync(async () =>
        {
            await provider.AcquireAsync(
                new ProjectLifecycleLockRequest(missingProjectRoot),
                InitialAcquireTimeout,
                CancellationToken.None);
        });

        Assert.IsType<DirectoryNotFoundException>(exception);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WithBrokenProjectRootSymbolicLink_ThrowsDirectoryNotFoundException ()
    {
        using var scope = CreatePhysicalResolutionScope("broken-project-link");
        var brokenProjectRoot = scope.CreateDirectorySymbolicLink(
            "broken-project",
            Path.Combine(scope.FullPath, "missing", "UnityProject"));
        var provider = CreateProvider(scope, new FakeTimeProvider(DateTimeOffset.UnixEpoch));
        var exception = await Record.ExceptionAsync(async () =>
        {
            await provider.AcquireAsync(
                new ProjectLifecycleLockRequest(AbsolutePath.Parse(brokenProjectRoot)),
                InitialAcquireTimeout,
                CancellationToken.None);
        });

        Assert.IsType<DirectoryNotFoundException>(exception);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Acquire_WithSymbolicLinkCycle_ThrowsIOException ()
    {
        using var scope = CreatePhysicalResolutionScope("project-link-cycle");
        var firstLinkPath = Path.Combine(scope.FullPath, "first-link");
        var secondLinkPath = Path.Combine(scope.FullPath, "second-link");
        scope.CreateDirectorySymbolicLink("first-link", secondLinkPath);
        scope.CreateDirectorySymbolicLink("second-link", firstLinkPath);
        var provider = CreateProvider(scope, new FakeTimeProvider(DateTimeOffset.UnixEpoch));

        var exception = await Record.ExceptionAsync(async () =>
        {
            await provider.AcquireAsync(
                new ProjectLifecycleLockRequest(AbsolutePath.Parse(firstLinkPath)),
                InitialAcquireTimeout,
                CancellationToken.None);
        });

        Assert.IsType<IOException>(exception);
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
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var lockStorageRoot = AbsolutePath.Parse(Path.Combine(scope.FullPath, "locks", "unity-projects"));
        var provider = new FileSystemProjectLifecycleLockProvider(timeProvider, lockStorageRoot);
        var handle = await provider.AcquireAsync(
            new ProjectLifecycleLockRequest(AbsolutePath.Parse(scope.CreateDirectory("UnityProject"))),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        await handle.DisposeAsync();

        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(lockStorageRoot.Value);
        var lockKeyDirectoryPath = Assert.Single(Directory.EnumerateDirectories(lockStorageRoot.Value));
        PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(lockKeyDirectoryPath);
    }

    private static FileSystemProjectLifecycleLockProvider CreateProvider (
        TestDirectoryScope scope,
        TimeProvider timeProvider)
    {
        return new FileSystemProjectLifecycleLockProvider(
            timeProvider,
            AbsolutePath.Parse(scope.CreateDirectory("locks")));
    }

    private static FileSystemProjectLifecycleLockProvider CreateProvider (
        PhysicalResolutionScope scope,
        TimeProvider timeProvider)
    {
        return new FileSystemProjectLifecycleLockProvider(
            timeProvider,
            AbsolutePath.Parse(scope.CreateDirectory("locks")));
    }

    private static PhysicalResolutionScope CreatePhysicalResolutionScope (string testCaseName)
    {
        var root = Path.Combine(
            TestRepositoryPaths.GetFullPath("TestResults"),
            "daemon-lock",
            testCaseName,
            Guid.NewGuid().ToString("N"));
        return new PhysicalResolutionScope(root);
    }

    private static string CreateExpectedLockKey (AbsolutePath actualCasedProjectRoot)
    {
        var identityText = CreateExpectedLockIdentityText(actualCasedProjectRoot);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identityText))).ToLowerInvariant();
    }

    private static string CreateExpectedLockIdentityText (AbsolutePath actualCasedProjectRoot)
    {
        if (!OperatingSystem.IsWindows())
        {
            return actualCasedProjectRoot.Value;
        }

        var root = actualCasedProjectRoot.GetRoot();
        return string.Concat(
            root.Value.ToUpperInvariant(),
            actualCasedProjectRoot.Value[root.Value.Length..]);
    }

    private static async Task AssertSecondAcquireWaitsForReleaseAsync (
        FileSystemProjectLifecycleLockProvider firstProvider,
        FileSystemProjectLifecycleLockProvider secondProvider,
        FakeTimeProvider timeProvider,
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
            var secondHandle = await secondAcquireTask.WaitAsync(AcquireWaitTimeout);
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

    private static string CreateRootCaseVariantPath (string path)
    {
        var rootPath = Path.GetPathRoot(path);
        Assert.False(string.IsNullOrWhiteSpace(rootPath));
        var characters = rootPath.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];
            if (!char.IsLetter(character))
            {
                continue;
            }

            characters[index] = char.IsUpper(character)
                ? char.ToLowerInvariant(character)
                : char.ToUpperInvariant(character);
            var caseVariantRoot = new string(characters);
            return caseVariantRoot + path[rootPath.Length..];
        }

        throw new InvalidOperationException($"Filesystem root contains no letter whose case can be changed: {rootPath}");
    }

    private static async Task<WindowsJunctionCreationResult> CreateWindowsJunctionAsync (
        string junctionPath,
        string targetPath)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var command = $"{processStartInfo.FileName} {processStartInfo.Arguments}";
        try
        {
            using var process = Process.Start(processStartInfo);
            if (process is null)
            {
                return WindowsJunctionCreationResult.FailedToStart(command, junctionPath, targetPath);
            }

            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            Exception? processException = null;
            try
            {
                await process.WaitForExitAsync().WaitAsync(AcquireWaitTimeout);
            }
            catch (Exception exception)
            {
                processException = exception;
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync().WaitAsync(AcquireWaitTimeout);
                    }
                    catch (Exception terminationException)
                    {
                        processException = new AggregateException(exception, terminationException);
                    }
                }
            }

            if (!process.HasExited)
            {
                return WindowsJunctionCreationResult.FailedToRun(
                    command,
                    junctionPath,
                    targetPath,
                    processException ?? new InvalidOperationException("Junction creation process did not exit."));
            }

            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;
            return new WindowsJunctionCreationResult(
                command,
                junctionPath,
                targetPath,
                process.ExitCode,
                standardOutput,
                standardError,
                processException is null && process.ExitCode == 0 && Directory.Exists(junctionPath),
                processException);
        }
        catch (Exception exception)
        {
            return WindowsJunctionCreationResult.FailedToRun(
                command,
                junctionPath,
                targetPath,
                exception);
        }
    }

    private sealed record WindowsJunctionCreationResult (
        string Command,
        string JunctionPath,
        string TargetPath,
        int? ExitCode,
        string StandardOutput,
        string StandardError,
        bool Succeeded,
        Exception? Exception)
    {
        public static WindowsJunctionCreationResult FailedToStart (
            string command,
            string junctionPath,
            string targetPath)
        {
            return new WindowsJunctionCreationResult(
                command,
                junctionPath,
                targetPath,
                null,
                string.Empty,
                string.Empty,
                false,
                null);
        }

        public static WindowsJunctionCreationResult FailedToRun (
            string command,
            string junctionPath,
            string targetPath,
            Exception exception)
        {
            return new WindowsJunctionCreationResult(
                command,
                junctionPath,
                targetPath,
                null,
                string.Empty,
                string.Empty,
                false,
                exception);
        }

        public string CreateFailureMessage (Exception? cleanupException = null)
        {
            return $"""
                Windows junction fixture creation failed.
                Command: {Command}
                Exit code: {ExitCode?.ToString() ?? "not available"}
                Target path: {TargetPath}
                Junction path: {JunctionPath}
                Standard output:
                {StandardOutput}
                Standard error:
                {StandardError}
                Exception: {Exception?.ToString() ?? "none"}
                Cleanup exception: {cleanupException?.ToString() ?? "none"}
                """;
        }
    }

    private sealed class PhysicalResolutionScope : IDisposable
    {
        private readonly Stack<string> directoryLinkPaths = new();
        private bool isDisposed;

        public PhysicalResolutionScope (string fullPath)
        {
            FullPath = fullPath;
            Directory.CreateDirectory(FullPath);
        }

        public string FullPath { get; }

        public string CreateDirectory (string relativePath)
        {
            var fullPath = Path.Combine(FullPath, relativePath);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        public string CreateDirectorySymbolicLink (string relativePath, string targetPath)
        {
            var linkPath = Path.Combine(FullPath, relativePath);
            Directory.CreateSymbolicLink(linkPath, targetPath);
            RegisterDirectoryLink(linkPath);
            return linkPath;
        }

        public void RegisterDirectoryLink (string linkPath)
        {
            var containedLinkPath = ContainedPath.Create(
                AbsolutePath.Parse(FullPath),
                AbsolutePath.Parse(linkPath));
            directoryLinkPaths.Push(containedLinkPath.Target.Value);
        }

        public void Dispose ()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            var cleanupExceptions = new List<Exception>();
            while (directoryLinkPaths.TryPop(out var directoryLinkPath))
            {
                try
                {
                    Directory.Delete(directoryLinkPath, recursive: false);
                }
                catch (DirectoryNotFoundException)
                {
                    // The test or a failed fixture setup already removed the link.
                }
                catch (Exception exception)
                {
                    cleanupExceptions.Add(exception);
                }
            }

            if (Directory.Exists(FullPath))
            {
                try
                {
                    Directory.Delete(FullPath, recursive: true);
                }
                catch (Exception exception)
                {
                    cleanupExceptions.Add(exception);
                }
            }

            if (cleanupExceptions.Count > 0)
            {
                throw new AggregateException(
                    $"Physical path resolution fixture cleanup failed: {FullPath}",
                    cleanupExceptions);
            }
        }
    }
}

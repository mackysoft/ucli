using System.Diagnostics;
using System.Runtime.Versioning;
using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class FileExclusiveLockTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task AcquireAsync_WhenOwnerRetainsSameLock_TimesOut ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "exclusive-lock-timeout");
        var lockPath = AbsolutePath.Parse(Path.Combine(scope.FullPath, "session.lock"));
        using var owner = FileExclusiveLock.Acquire(
            lockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            using var contender = await FileExclusiveLock.AcquireAsync(
                lockPath,
                TimeSpan.FromMilliseconds(50),
                CancellationToken.None);
        });

        Assert.Contains(lockPath.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AcquireAsync_WhenWaitingIsCanceled_ReleasesWaiterForNextOwner ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "exclusive-lock-cancellation");
        var lockPath = AbsolutePath.Parse(Path.Combine(scope.FullPath, "session.lock"));
        using var owner = FileExclusiveLock.Acquire(
            lockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        using var cancellationTokenSource = new CancellationTokenSource();
        var contenderTask = FileExclusiveLock.AcquireAsync(
                lockPath,
                TimeSpan.FromSeconds(1),
                cancellationTokenSource.Token)
            .AsTask();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            using var contender = await contenderTask;
        });

        owner.Dispose();
        using var nextOwner = await FileExclusiveLock.AcquireAsync(
            lockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Acquire_WhenLockPathIsSymbolicLink_RejectsLinkWithoutMutatingTarget ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "exclusive-lock-symlink");
        var targetPath = scope.WriteFile("target.txt", "target-contents");
        var lockPath = AbsolutePath.Parse(scope.GetPath("session.lock"));
        if (!TestSymbolicLinks.TryCreateFile(lockPath.Value, targetPath))
        {
            return;
        }

        var exception = Assert.Throws<IOException>(() => FileExclusiveLock.Acquire(
            lockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None));

        Assert.Contains("reparse point", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("target-contents", File.ReadAllText(targetPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("macos")]
    public async Task Acquire_OnMacOS_PreventsChildProcessFromAcquiringSameRegion ()
    {
        const string lockfPath = "/usr/bin/lockf";
        const string truePath = "/usr/bin/true";
        if (!OperatingSystem.IsMacOS() || !File.Exists(lockfPath) || !File.Exists(truePath))
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "exclusive-lock-child-process");
        var lockPath = AbsolutePath.Parse(Path.Combine(scope.FullPath, "session.lock"));
        using var owner = FileExclusiveLock.Acquire(
            lockPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        var ownedResult = await RunLockfProbeAsync(lockfPath, truePath, lockPath.Value);

        Assert.NotEqual(0, ownedResult.ExitCode);
        owner.Dispose();

        var releasedResult = await RunLockfProbeAsync(lockfPath, truePath, lockPath.Value);

        Assert.True(releasedResult.ExitCode == 0, releasedResult.StandardError);
    }

    private static async Task<LockfProbeResult> RunLockfProbeAsync (
        string lockfPath,
        string commandPath,
        string lockPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = lockfPath,
            UseShellExecute = false,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-t");
        startInfo.ArgumentList.Add("0");
        startInfo.ArgumentList.Add(lockPath);
        startInfo.ArgumentList.Add(commandPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("lockf probe process could not be started.");
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new LockfProbeResult(process.ExitCode, await standardErrorTask);
    }

    private readonly record struct LockfProbeResult (
        int ExitCode,
        string StandardError);
}

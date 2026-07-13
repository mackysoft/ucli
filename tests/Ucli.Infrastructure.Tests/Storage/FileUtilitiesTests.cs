using System.Runtime.Versioning;
using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class FileUtilitiesTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ReadAllTextOrNull_WhenFileExists_ReadsContents ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "reopen-safe-sync-read");
        var path = scope.WriteFile("session.json", "session-contents");

        var contents = FileUtilities.ReadAllTextOrNull(path);

        Assert.Equal("session-contents", contents);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadBytesOrNullWithinLimitAsync_WhenFileFits_ReturnsExactBytes ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "bounded-read-fits");
        var path = Path.Combine(scope.FullPath, "session.json");
        var expected = new byte[] { 0x00, 0x7f, 0x80, 0xff };
        await File.WriteAllBytesAsync(path, expected, CancellationToken.None);

        var contents = await FileUtilities.ReadBytesOrNullWithinLimitAsync(
            path,
            expected.Length,
            CancellationToken.None);

        Assert.NotNull(contents);
        Assert.Equal(expected, contents.Value.ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadBytesOrNullWithinLimitAsync_WhenFileExceedsLimit_ThrowsIOException ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "bounded-read-exceeds");
        var path = Path.Combine(scope.FullPath, "session.json");
        await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3, 4 }, CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() => FileUtilities.ReadBytesOrNullWithinLimitAsync(
                path,
                maximumBytes: 3,
                CancellationToken.None)
            .AsTask());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 15)]
    [InlineData(20, 100)]
    public void ResolveFileReplacementRetryDelay_WithWindowsSharingViolation_UsesBoundedBackoff (
        int failureCount,
        int expectedDelayMilliseconds)
    {
        var exception = new IOExceptionWithHResult(unchecked((int)0x80070020));

        var delay = FileUtilities.ResolveFileReplacementRetryDelay(exception, failureCount);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedDelayMilliseconds), delay);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveFileReplacementRetryDelay_AfterRetryLimit_DoesNotRetry ()
    {
        var exception = new IOExceptionWithHResult(unchecked((int)0x80070020));

        var delay = FileUtilities.ResolveFileReplacementRetryDelay(exception, failureCount: 21);

        Assert.Null(delay);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveFileReplacementRetryDelay_WithOtherIoFailure_DoesNotRetry ()
    {
        var exception = new IOExceptionWithHResult(unchecked((int)0x80070005));

        var delay = FileUtilities.ResolveFileReplacementRetryDelay(exception, failureCount: 1);

        Assert.Null(delay);
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("windows")]
    public void OpenReopenSafeReadStream_OnWindows_AllowsAtomicReplacementWhileReadHandleRemainsOpen ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "reopen-safe-read");
        var path = scope.WriteFile("lifecycle.json", "old-contents");
        var temporaryPath = scope.WriteFile("lifecycle.json.tmp", "new-contents");

        using var stream = FileUtilities.OpenReopenSafeReadStream(path);
        File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);

        using var reader = new StreamReader(stream);
        Assert.Equal("old-contents", reader.ReadToEnd());
        Assert.Equal("new-contents", File.ReadAllText(path));
    }

    [Theory]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("windows")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteAllTextAtomically_OnWindows_RetriesBriefSharingViolation (bool useAsyncApi)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "atomic-write-sharing-violation");
        var path = scope.WriteFile("lifecycle.json", "old-contents");
        Task writeTask;
        bool observationCompleted;
        bool temporaryFileObserved;
        bool completedWhileLocked;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            writeTask = useAsyncApi
                ? FileUtilities.WriteAllTextAtomicallyAsync(path, "new-contents").AsTask()
                : Task.Run(() => FileUtilities.WriteAllTextAtomically(path, "new-contents"));

            observationCompleted = SpinWait.SpinUntil(
                () => writeTask.IsCompleted || Directory.GetFiles(scope.FullPath).Length > 1,
                TimeSpan.FromSeconds(5));
            temporaryFileObserved = Directory.GetFiles(scope.FullPath).Length > 1;

            if (temporaryFileObserved && !writeTask.IsCompleted)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            completedWhileLocked = writeTask.IsCompleted;
        }

        var exception = await Record.ExceptionAsync(
            () => writeTask.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.True(observationCompleted);
        Assert.True(temporaryFileObserved);
        Assert.False(completedWhileLocked);
        Assert.Null(exception);
        Assert.Equal("new-contents", File.ReadAllText(path));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteAllTextAtomically_WhenTargetExists_ReplacesExistingContents ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "atomic-write-overwrite");
        var path = Path.Combine(scope.FullPath, "daemon-diagnosis.json");
        await File.WriteAllTextAsync(path, "old-contents", CancellationToken.None);

        await FileUtilities.WriteAllTextAtomicallyAsync(path, "new-contents", CancellationToken.None);

        var contents = await File.ReadAllTextAsync(path, CancellationToken.None);
        Assert.Equal("new-contents", contents);

        var files = Directory.GetFiles(scope.FullPath);
        Assert.Single(files);
        Assert.Equal(Path.GetFullPath(path), Path.GetFullPath(files[0]));
    }

    private sealed class IOExceptionWithHResult : IOException
    {
        public IOExceptionWithHResult (int hResult)
        {
            HResult = hResult;
        }
    }

}

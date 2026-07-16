using System.Diagnostics;
using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests;

public sealed class EditorLogTemporaryFilePathTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void OpenExclusiveWrite_ReservesCurrentProcessOwnedSiblingFile ()
    {
        using var scope = TestDirectories.CreateTempScope("editor-log-temporary-file", "create");
        var destinationPath = scope.GetPath("editor.log");
        using var process = Process.GetCurrentProcess();

        string temporaryPath;
        using (var stream = EditorLogTemporaryFilePath.OpenExclusiveWrite(
                   destinationPath,
                   bufferSize: 4096,
                   out temporaryPath))
        {
            Assert.True(File.Exists(temporaryPath));
            Assert.Throws<IOException>(() => new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None));
            stream.WriteByte(1);
        }

        Assert.True(EditorLogTemporaryFilePath.TryGetOwnerProcessId(
            Path.GetFileName(temporaryPath),
            out var processId));
        Assert.Equal(process.Id, processId);
        Assert.Equal(
            Path.GetFullPath(scope.FullPath),
            Path.GetFullPath(Path.GetDirectoryName(temporaryPath)!));
        Assert.StartsWith(".tmp-", Path.GetFileName(temporaryPath), StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFileName(destinationPath), Path.GetFileName(temporaryPath), StringComparison.Ordinal);
        Assert.True(Path.GetFileName(temporaryPath).Length <= EditorLogTemporaryFilePath.MaximumFileNameLength);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetOwnerProcessId_WithMalformedCandidate_ReturnsFalse ()
    {
        const string nonce = "0123456789abcd";
        var candidates = new[]
        {
            ".tmp-abcdefgh.ijk",
            ".tmp123-" + nonce,
            ".tmp--" + nonce,
            ".tmp--1-" + nonce,
            ".tmp-0-" + nonce,
            ".tmp-01-" + nonce,
            ".tmp-not-a-pid-" + nonce,
            ".tmp-123",
            ".tmp-123-",
            ".tmp-123-not-a-hex-nonce",
            ".tmp-123-0123456789abCd",
            ".tmp-123-0123456789abc",
            ".tmp-123-0123456789abcde",
            ".tmp-123-0123456789abcd-extra",
            "editor.log.tmp.123." + Guid.NewGuid().ToString("N"),
        };

        foreach (var candidate in candidates)
        {
            Assert.False(EditorLogTemporaryFilePath.TryGetOwnerProcessId(
                candidate,
                out _));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task OpenExclusiveWrite_CreatesFileAcceptedByAtomicPublication ()
    {
        using var scope = TestDirectories.CreateTempScope("editor-log-temporary-file", "publish");
        var destinationPath = scope.GetPath("editor.log");

        string temporaryPath;
        await using (var stream = EditorLogTemporaryFilePath.OpenExclusiveWrite(
                         destinationPath,
                         bufferSize: 4096,
                         out temporaryPath))
        {
            await stream.WriteAsync("editor log"u8.ToArray());
        }

        await FileUtilities.PublishAtomicWriteTemporaryFileAsync(
            temporaryPath,
            destinationPath,
            CancellationToken.None);

        Assert.False(File.Exists(temporaryPath));
        Assert.Equal("editor log", await File.ReadAllTextAsync(destinationPath));
    }
}

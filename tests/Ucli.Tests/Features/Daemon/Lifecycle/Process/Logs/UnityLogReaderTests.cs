using MackySoft.Ucli.Infrastructure.Storage;
namespace MackySoft.Ucli.Tests.Daemon;

using System.Text;
using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class UnityLogReaderTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadTail_WhenLogFileDoesNotExist_ReturnsEmptyText ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-log-reader", "missing-log");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var logReader = new UnityLogReader();

        var readResult = await logReader.ReadTailAsync(storageRoot, ProjectFingerprintTestFactory.Create("fingerprint-missing"), cancellationToken: CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal(string.Empty, readResult.Text);
        Assert.False(readResult.Truncated);
        Assert.Equal(0, readResult.SizeBytes);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadTail_ReturnsTailBytesAndTruncatedFlag ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-log-reader", "tail-bytes");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-tail");
        var logReader = new UnityLogReader();
        var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(storageRoot, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(unityLogPath.Value)!);

        var content = "line-1\nline-2\nline-3\n";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        await File.WriteAllTextAsync(unityLogPath.Value, content, CancellationToken.None);

        var readResult = await logReader.ReadTailAsync(storageRoot, projectFingerprint, maxBytes: 8, cancellationToken: CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.True(readResult.Truncated);
        Assert.Equal(contentBytes.Length, readResult.SizeBytes);
        Assert.Equal("\nline-3\n", readResult.Text);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadTail_WhenMaxBytesIsNotPositive_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-log-reader", "invalid-max-bytes");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var logReader = new UnityLogReader();

        var readResult = await logReader.ReadTailAsync(storageRoot, ProjectFingerprintTestFactory.Create("fingerprint-invalid"), maxBytes: 0, cancellationToken: CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("maxBytes", error.Message, StringComparison.Ordinal);
    }
}

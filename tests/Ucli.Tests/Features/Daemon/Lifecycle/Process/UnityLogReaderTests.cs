using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Infrastructure.Storage;
namespace MackySoft.Ucli.Tests.Daemon;

using System.Text;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Shared.Foundation;

public sealed class UnityLogReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadTail_WhenLogFileDoesNotExist_ReturnsEmptyText ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-log-reader", "missing-log");
        var logReader = new UnityLogReader();

        var readResult = await logReader.ReadTail(scope.FullPath, "fingerprint-missing", cancellationToken: CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.Equal(string.Empty, readResult.Text);
        Assert.False(readResult.Truncated);
        Assert.Equal(0, readResult.SizeBytes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadTail_ReturnsTailBytesAndTruncatedFlag ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-log-reader", "tail-bytes");
        var projectFingerprint = "fingerprint-tail";
        var logReader = new UnityLogReader();
        var unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(scope.FullPath, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(unityLogPath)!);

        var content = "line-1\nline-2\nline-3\n";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        await File.WriteAllTextAsync(unityLogPath, content, CancellationToken.None);

        var readResult = await logReader.ReadTail(scope.FullPath, projectFingerprint, maxBytes: 8, cancellationToken: CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        Assert.True(readResult.Truncated);
        Assert.Equal(contentBytes.Length, readResult.SizeBytes);
        Assert.Equal("\nline-3\n", readResult.Text);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadTail_WhenMaxBytesIsNotPositive_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-log-reader", "invalid-max-bytes");
        var logReader = new UnityLogReader();

        var readResult = await logReader.ReadTail(scope.FullPath, "fingerprint-invalid", maxBytes: 0, cancellationToken: CancellationToken.None);

        Assert.False(readResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(readResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("maxBytes", error.Message, StringComparison.Ordinal);
    }
}

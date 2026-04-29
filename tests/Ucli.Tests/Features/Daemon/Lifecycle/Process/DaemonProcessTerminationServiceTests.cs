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
namespace MackySoft.Ucli.Tests.Daemon;

using System.Diagnostics;
using MackySoft.Ucli.Shared.Foundation;

public sealed class DaemonProcessTerminationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStopped_WhenProcessIdIsNull_ReturnsSuccess ()
    {
        var service = CreateService();

        var result = await service.EnsureStopped(
            processId: null,
            expectedIssuedAtUtc: null,
            timeout: TimeSpan.FromMilliseconds(100),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStopped_WhenProcessIdentityCannotBeVerified_ReturnsFailureWithoutKilling ()
    {
        var service = CreateService();
        var currentProcessId = Environment.ProcessId;

        var result = await service.EnsureStopped(
            processId: currentProcessId,
            expectedIssuedAtUtc: null,
            timeout: TimeSpan.FromMilliseconds(100),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("identity could not be verified", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStopped_WhenProcessStartTimeDoesNotMatchSessionIssuedAt_ReturnsFailure ()
    {
        var service = CreateService();
        var currentProcess = Process.GetCurrentProcess();

        var result = await service.EnsureStopped(
            processId: currentProcess.Id,
            expectedIssuedAtUtc: DateTimeOffset.UtcNow.AddHours(1),
            timeout: TimeSpan.FromMilliseconds(100),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("identity mismatch", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static DaemonProcessTerminationService CreateService ()
    {
        return new DaemonProcessTerminationService(new DaemonProcessIdentityAssessor());
    }
}

using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonCleanupServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenOperationReturnsCompleted_ReturnsCompletedOutput ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3400);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var operation = new DaemonServiceTestContext.StubDaemonCleanupOperation
        {
            CleanupResult = DaemonCleanupResult.Completed(),
        };
        var service = new DaemonCleanupService(resolver, operation);

        var result = await service.Cleanup(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonCleanupExecutionOutput>(result.Output);
        Assert.Equal(DaemonCleanupStatus.Completed, output.CleanupStatus);
        Assert.Equal(DaemonCleanupSkipReason.None, output.SkipReason);
        Assert.Equal(3400, output.TimeoutMilliseconds);
        Assert.Equal(1, operation.CleanupCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenOperationReturnsSkipped_MapsSkipReason ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3100);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var operation = new DaemonServiceTestContext.StubDaemonCleanupOperation
        {
            CleanupResult = DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.UnsafeInvalidSession),
        };
        var service = new DaemonCleanupService(resolver, operation);

        var result = await service.Cleanup(projectPath: "/tmp/unity-project", timeout: "3100", cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonCleanupExecutionOutput>(result.Output);
        Assert.Equal(DaemonCleanupStatus.Skipped, output.CleanupStatus);
        Assert.Equal(DaemonCleanupSkipReason.UnsafeInvalidSession, output.SkipReason);
        Assert.Equal(UcliCommandIds.DaemonCleanup, resolver.LastTimeoutCommand);
        Assert.Equal("/tmp/unity-project", resolver.LastProjectPath);
        Assert.Equal("3100", resolver.LastTimeoutOption);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenExecutionContextResolutionFails_ReturnsFailureWithoutOperationCall ()
    {
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(
                ExecutionError.InvalidArgument("invalid project path")));
        var operation = new DaemonServiceTestContext.StubDaemonCleanupOperation();
        var service = new DaemonCleanupService(resolver, operation);

        var result = await service.Cleanup(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(0, operation.CleanupCallCount);
    }
}

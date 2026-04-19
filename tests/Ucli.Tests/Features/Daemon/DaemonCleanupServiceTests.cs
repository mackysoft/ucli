using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Features.Daemon.Runtime;
using MackySoft.Ucli.Features.Daemon.Services;
using MackySoft.Ucli.Shared.Foundation;

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
        Assert.Equal(DaemonCleanupStateCodec.Completed, output.CleanupStatus);
        Assert.Null(output.SkipReason);
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
        Assert.Equal(DaemonCleanupStateCodec.Skipped, output.CleanupStatus);
        Assert.Equal(DaemonCleanupSkipReasonCodec.UnsafeInvalidSession, output.SkipReason);
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
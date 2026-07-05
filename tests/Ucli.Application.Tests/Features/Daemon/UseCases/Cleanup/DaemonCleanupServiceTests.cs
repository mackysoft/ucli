using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonCleanupServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenOperationReturnsCompleted_ReturnsCompletedOutput ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 3400);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var operation = new RecordingDaemonCleanupOperation(DaemonCleanupResult.Completed());
        var service = new DaemonCleanupService(resolver, operation);

        var result = await service.CleanupAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonCleanupExecutionOutput>(result.Output);
        Assert.Equal(DaemonCleanupStatus.Completed, output.CleanupStatus);
        Assert.Equal(DaemonCleanupSkipReason.None, output.SkipReason);
        Assert.Equal(0, output.DeletedLaunchAttemptCount);
        Assert.Equal(3400, output.TimeoutMilliseconds);
        DaemonCleanupOperationAssert.CleanupRequestedOnce(
            operation,
            context,
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenArgumentsAreSpecified_PropagatesContextAndMapsSkipReason ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 3100);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var operation = new RecordingDaemonCleanupOperation(
            DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.UnsafeInvalidSession));
        var service = new DaemonCleanupService(resolver, operation);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await service.CleanupAsync(
            projectPath: "/tmp/unity-project",
            timeoutMilliseconds: 3100,
            cancellationToken: cancellationToken);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonCleanupExecutionOutput>(result.Output);
        Assert.Equal(DaemonCleanupStatus.Skipped, output.CleanupStatus);
        Assert.Equal(DaemonCleanupSkipReason.UnsafeInvalidSession, output.SkipReason);
        Assert.Equal(0, output.DeletedLaunchAttemptCount);
        DaemonCommandExecutionContextResolverAssert.ResolvedFor(
            resolver,
            UcliCommandIds.DaemonCleanup,
            expectedProjectPath: "/tmp/unity-project",
            expectedTimeoutMilliseconds: 3100,
            expectedCancellationToken: cancellationToken);
        DaemonCleanupOperationAssert.CleanupRequestedOnce(
            operation,
            context,
            cancellationToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenExecutionContextResolutionFails_ReturnsFailureWithoutOperationCall ()
    {
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(
                ExecutionError.InvalidArgument("invalid project path")));
        var operation = new RecordingDaemonCleanupOperation(DaemonCleanupResult.Completed());
        var service = new DaemonCleanupService(resolver, operation);

        var result = await service.CleanupAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        DaemonCleanupOperationAssert.ContextResolutionFailureStoppedBeforeCleanup(
            result,
            operation,
            ExecutionErrorKind.InvalidArgument);
    }
}

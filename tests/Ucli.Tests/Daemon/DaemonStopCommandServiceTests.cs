using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonStopCommandServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenDaemonIsNotRunning_ReturnsNotRunningOutputWithNullSession ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3456);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonCommandServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var service = new DaemonStopCommandService(resolver, daemonStopOperation);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStopExecutionOutput>(result.Output);
        Assert.Equal("notRunning", output.StopStatus);
        Assert.Equal("notRunning", output.DaemonStatus);
        Assert.Equal(3456, output.TimeoutMilliseconds);
        Assert.Null(output.Session);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(1, daemonStopOperation.StopCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenExecutionContextResolutionFails_ReturnsFailureWithoutDaemonCall ()
    {
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(
                ExecutionError.InvalidArgument("invalid project path")));
        var daemonStopOperation = new DaemonCommandServiceTestContext.StubDaemonStopOperation();
        var service = new DaemonStopCommandService(resolver, daemonStopOperation);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(0, daemonStopOperation.StopCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenDaemonStopOperationFailsWithoutError_ReturnsFallbackInternalError ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1100);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonCommandServiceTestContext.StubDaemonStopOperation
        {
            StopResult = new DaemonStopResult(DaemonStopStatus.Failed, null),
        };
        var service = new DaemonStopCommandService(resolver, daemonStopOperation);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Daemon stop operation failed without structured error details.", error.Message);
        Assert.Equal(1, daemonStopOperation.StopCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenArgumentsAreSpecified_PropagatesToResolverAndOperation ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2100);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonCommandServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var service = new DaemonStopCommandService(resolver, daemonStopOperation);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await service.Stop(
            projectPath: "/tmp/sandbox-unity",
            timeout: "8888",
            cancellationToken: cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(UcliCommandIds.DaemonStop, resolver.LastTimeoutCommand);
        Assert.Equal("/tmp/sandbox-unity", resolver.LastProjectPath);
        Assert.Equal("8888", resolver.LastTimeoutOption);
        Assert.Equal(cancellationToken, resolver.LastCancellationToken);
        Assert.Equal(context.Context.UnityProject, daemonStopOperation.LastUnityProject);
        Assert.Equal(context.Timeout, daemonStopOperation.LastTimeout);
        Assert.Equal(cancellationToken, daemonStopOperation.LastCancellationToken);
    }
}
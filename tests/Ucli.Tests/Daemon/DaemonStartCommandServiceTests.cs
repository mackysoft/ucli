using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonStartCommandServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenDaemonStarts_ReturnsRunningOutputWithMappedSession ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1200);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStartOperation = new DaemonCommandServiceTestContext.StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.Started(DaemonCommandServiceTestContext.CreateSession()),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper
        {
            Output = DaemonCommandServiceTestContext.CreateSessionOutput(),
        };
        var service = new DaemonStartCommandService(resolver, daemonStartOperation, mapper);

        var result = await service.Start(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStartExecutionOutput>(result.Output);
        Assert.Equal("started", output.StartStatus);
        Assert.Equal("running", output.DaemonStatus);
        Assert.Equal(1200, output.TimeoutMilliseconds);
        Assert.Equal(mapper.Output, output.Session);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(1, daemonStartOperation.StartCallCount);
        Assert.Equal(1, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExecutionContextResolutionFails_ReturnsFailureWithoutDaemonCall ()
    {
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(
                ExecutionError.InvalidArgument("invalid project path")));
        var daemonStartOperation = new DaemonCommandServiceTestContext.StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.Started(DaemonCommandServiceTestContext.CreateSession()),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = new DaemonStartCommandService(resolver, daemonStartOperation, mapper);

        var result = await service.Start(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(0, daemonStartOperation.StartCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenDaemonStartOperationFails_ReturnsFailure ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1600);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStartOperation = new DaemonCommandServiceTestContext.StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.Failure(ExecutionError.InternalError("start failed")),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = new DaemonStartCommandService(resolver, daemonStartOperation, mapper);

        var result = await service.Start(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("start failed", error.Message);
        Assert.Equal(1, daemonStartOperation.StartCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenDaemonStartOperationFailsWithoutError_ReturnsFallbackInternalError ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 900);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStartOperation = new DaemonCommandServiceTestContext.StubDaemonStartOperation
        {
            StartResult = new DaemonStartResult(DaemonStartStatus.Failed, null, null),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = new DaemonStartCommandService(resolver, daemonStartOperation, mapper);

        var result = await service.Start(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Daemon start operation failed without structured error details.", error.Message);
        Assert.Equal(1, daemonStartOperation.StartCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenArgumentsAreSpecified_PropagatesToResolverAndOperation ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1700);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStartOperation = new DaemonCommandServiceTestContext.StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.Started(DaemonCommandServiceTestContext.CreateSession()),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = new DaemonStartCommandService(resolver, daemonStartOperation, mapper);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await service.Start(
            projectPath: "/tmp/sandbox-unity",
            timeout: "7777",
            cancellationToken: cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(UcliCommandIds.DaemonStart, resolver.LastTimeoutCommand);
        Assert.Equal("/tmp/sandbox-unity", resolver.LastProjectPath);
        Assert.Equal("7777", resolver.LastTimeoutOption);
        Assert.Equal(cancellationToken, resolver.LastCancellationToken);
        Assert.Equal(context.Context.UnityProject, daemonStartOperation.LastUnityProject);
        Assert.Equal(context.Timeout, daemonStartOperation.LastTimeout);
        Assert.Equal(cancellationToken, daemonStartOperation.LastCancellationToken);
    }
}
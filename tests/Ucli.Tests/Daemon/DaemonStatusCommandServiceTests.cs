using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonStatusCommandServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonIsStale_ReturnsStaleOutputWithMappedSession ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 5678);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Stale(DaemonCommandServiceTestContext.CreateSession()),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper
        {
            Output = DaemonCommandServiceTestContext.CreateSessionOutput(),
        };
        var service = new DaemonStatusCommandService(resolver, daemonStatusOperation, mapper);

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal("stale", output.DaemonStatus);
        Assert.Equal(5678, output.TimeoutMilliseconds);
        Assert.Equal(mapper.Output, output.Session);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(1, daemonStatusOperation.GetStatusCallCount);
        Assert.Equal(1, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonStatusOperationFails_ReturnsFailure ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1000);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Failure(ExecutionError.InternalError("status failed")),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = new DaemonStatusCommandService(resolver, daemonStatusOperation, mapper);

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("status failed", error.Message);
        Assert.Equal(1, daemonStatusOperation.GetStatusCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonIsNotRunning_ReturnsOutputWithoutSession ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2222);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.NotRunning(),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = new DaemonStatusCommandService(resolver, daemonStatusOperation, mapper);

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal("notRunning", output.DaemonStatus);
        Assert.Equal(2222, output.TimeoutMilliseconds);
        Assert.Null(output.Session);
        Assert.Equal(1, daemonStatusOperation.GetStatusCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonStatusOperationFailsWithoutError_ReturnsFallbackInternalError ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1300);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = new DaemonStatusResult(DaemonStatusKind.Failed, null, null),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = new DaemonStatusCommandService(resolver, daemonStatusOperation, mapper);

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Daemon status operation failed without structured error details.", error.Message);
        Assert.Equal(1, daemonStatusOperation.GetStatusCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenStatusLiteralIsUnsupported_ReturnsInternalError ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1400);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = new DaemonStatusResult(
                Status: (DaemonStatusKind)int.MaxValue,
                Session: DaemonCommandServiceTestContext.CreateSession(),
                Error: null),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = new DaemonStatusCommandService(resolver, daemonStatusOperation, mapper);

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal($"Daemon status returned unsupported status: {(DaemonStatusKind)int.MaxValue}.", error.Message);
        Assert.Equal(1, daemonStatusOperation.GetStatusCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenArgumentsAreSpecified_PropagatesToResolverAndOperation ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2300);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.NotRunning(),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = new DaemonStatusCommandService(resolver, daemonStatusOperation, mapper);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await service.GetStatus(
            projectPath: "/tmp/sandbox-unity",
            timeout: "9999",
            cancellationToken: cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("/tmp/sandbox-unity", resolver.LastProjectPath);
        Assert.Equal("9999", resolver.LastTimeoutOption);
        Assert.Equal(cancellationToken, resolver.LastCancellationToken);
        Assert.Equal(context.Context.UnityProject, daemonStatusOperation.LastUnityProject);
        Assert.Equal(context.Timeout, daemonStatusOperation.LastTimeout);
        Assert.Equal(cancellationToken, daemonStatusOperation.LastCancellationToken);
    }
}
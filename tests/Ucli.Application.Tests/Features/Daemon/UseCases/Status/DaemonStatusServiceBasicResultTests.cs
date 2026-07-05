using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStatusServiceBasicResultTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonIsStale_ReturnsStaleOutputWithMappedSession ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 5678);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create();
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Stale(session));
        var service = CreateService(
            resolver,
            daemonStatusOperation);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Stale, output.DaemonStatus);
        Assert.Equal(5678, output.TimeoutMilliseconds);
        DaemonServiceOutputAssert.SessionMatches(session, output.Session);
        Assert.Null(output.Diagnosis);
        Assert.Null(output.ServerVersion);
        Assert.False(output.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonStatusOperationFails_ReturnsFailure ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 1000);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.Failure(ExecutionError.InternalError("status failed")));
        var service = CreateService(
            resolver,
            daemonStatusOperation);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("status failed", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonStatusOperationTimesOut_ReturnsTimeoutFailure ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 1000);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.Failure(ExecutionError.Timeout("probe timeout")));
        var service = CreateService(
            resolver,
            daemonStatusOperation);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal("probe timeout", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonIsNotRunning_ReturnsOutputWithoutSession ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2222);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.NotRunning());
        var service = CreateService(
            resolver,
            daemonStatusOperation);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.NotRunning, output.DaemonStatus);
        Assert.Equal(2222, output.TimeoutMilliseconds);
        Assert.Null(output.Session);
        Assert.Null(output.Diagnosis);
        Assert.Null(output.ServerVersion);
        Assert.False(output.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonStatusOperationFailsWithoutError_ReturnsFallbackInternalError ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 1300);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            new DaemonStatusResult(DaemonStatusKind.Failed, null, null, null));
        var service = CreateService(
            resolver,
            daemonStatusOperation);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Daemon status operation failed without structured error details.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenStatusLiteralIsUnsupported_ReturnsInternalError ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 1400);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            new DaemonStatusResult(
                Status: (DaemonStatusKind)int.MaxValue,
                Session: DaemonSessionTestFactory.Create(),
                Diagnosis: null,
                Error: null));
        var service = CreateService(
            resolver,
            daemonStatusOperation);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal($"Daemon status returned unsupported status: {(DaemonStatusKind)int.MaxValue}.", error.Message);
    }
}

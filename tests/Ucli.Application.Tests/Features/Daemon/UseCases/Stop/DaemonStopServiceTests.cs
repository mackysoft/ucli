using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStopServiceTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Stop_WhenSupervisorIsUnavailable_FallsBackToDirectStopOperation ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-command-service", "stop-fallback-not-running");
        var context = DaemonCommandExecutionContextTestFactory.Create(
            timeoutMilliseconds: 3456,
            repositoryRoot: scope.FullPath);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new RecordingDaemonStopOperation(DaemonStopResult.NotRunning());
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            TryStopProjectResult = null,
        };
        var service = CreateService(resolver, supervisorProjectGateway, daemonStopOperation);

        var result = await service.StopAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStopExecutionOutput>(result.Output);
        Assert.Equal(DaemonStopStatus.NotRunning, output.StopStatus);
        Assert.Equal(DaemonStatusKind.NotRunning, output.DaemonStatus);
        Assert.Equal(3456, output.TimeoutMilliseconds);
        Assert.Null(output.Session);
        DaemonProjectLifecycleGatewayAssert.TryStopProjectRequested(
            supervisorProjectGateway,
            context.Context.UnityProject,
            context.Timeout);
        DaemonStopOperationAssert.StopRequested(daemonStopOperation, context);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Stop_WhenExecutionContextResolutionFails_ReturnsFailureWithoutStopCall ()
    {
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(
                ExecutionError.InvalidArgument("invalid project path")));
        var daemonStopOperation = new RecordingDaemonStopOperation(DaemonStopResult.Stopped());
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway();
        var service = CreateService(resolver, supervisorProjectGateway, daemonStopOperation);

        var result = await service.StopAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        DaemonStopServiceAssert.StopNotAttemptedAfterContextResolutionFailure(
            supervisorProjectGateway,
            daemonStopOperation);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Stop_WhenSupervisorGatewayFails_ReturnsFailureWithoutDirectFallback ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(
            timeoutMilliseconds: 2100,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new RecordingDaemonStopOperation(DaemonStopResult.NotRunning());
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            TryStopProjectResult = DaemonStopResult.Failure(ExecutionError.InvalidArgument("invalid manifest")),
        };
        var service = CreateService(resolver, supervisorProjectGateway, daemonStopOperation);

        var result = await service.StopAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        DaemonStopServiceAssert.SupervisorStopFailureStoppedBeforeDirectFallback(
            supervisorProjectGateway,
            daemonStopOperation,
            context);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Stop_WhenSupervisorStopTimesOut_FallsBackToDirectStopOperation ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-command-service", "stop-supervisor-timeout-fallback");
        var timeProvider = new ManualTimeProvider();
        var context = DaemonCommandExecutionContextTestFactory.Create(
            timeoutMilliseconds: 15000,
            repositoryRoot: scope.FullPath);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new RecordingDaemonStopOperation(DaemonStopResult.Stopped());
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            TryStopProjectHandler = (_, timeout, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(timeout);
                return ValueTask.FromResult<DaemonStopResult?>(DaemonStopResult.Failure(ExecutionError.Timeout(
                    "supervisor stopProject timed out")));
            },
        };
        var service = CreateService(resolver, supervisorProjectGateway, daemonStopOperation, timeProvider: timeProvider);

        var result = await service.StopAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStopExecutionOutput>(result.Output);
        Assert.Equal(DaemonStopStatus.Stopped, output.StopStatus);
        var tryStopInvocation = DaemonProjectLifecycleGatewayAssert.TryStopProjectRequested(
            supervisorProjectGateway,
            context.Context.UnityProject,
            context.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(5), tryStopInvocation.Timeout);
        var directStopInvocation = DaemonStopOperationAssert.StopRequested(daemonStopOperation, context);
        Assert.True(directStopInvocation.Deadline.TryGetRemainingTimeout(out var directStopTimeout));
        Assert.True(directStopTimeout <= DaemonTimeouts.StopCompensationTimeout);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Stop_WhenSupervisorProbeConsumesBudget_PropagatesRemainingTimeoutToStopProject ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-command-service", "stop-remaining-timeout");
        var timeProvider = new ManualTimeProvider();
        var context = DaemonCommandExecutionContextTestFactory.Create(
            timeoutMilliseconds: 700,
            repositoryRoot: scope.FullPath);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new RecordingDaemonStopOperation(DaemonStopResult.NotRunning());
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            TryStopProjectHandler = (unityProject, timeout, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                return ValueTask.FromResult<DaemonStopResult?>(DaemonStopResult.Stopped());
            },
        };
        var service = CreateService(resolver, supervisorProjectGateway, daemonStopOperation, timeProvider: timeProvider);

        var result = await service.StopAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonStopServiceAssert.SupervisorStopCompletedWithoutDirectFallback(
            supervisorProjectGateway,
            daemonStopOperation,
            context);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Stop_WhenSupervisorIsReachable_UsesSupervisorStopProject ()
    {
        using var scope = TestDirectories.CreateTempScope("daemon-command-service", "stop-supervisor");
        var context = DaemonCommandExecutionContextTestFactory.Create(
            timeoutMilliseconds: 2100,
            repositoryRoot: scope.FullPath);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new RecordingDaemonStopOperation(DaemonStopResult.NotRunning());
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            TryStopProjectResult = DaemonStopResult.Stopped(),
        };
        var service = CreateService(resolver, supervisorProjectGateway, daemonStopOperation);

        var result = await service.StopAsync(
            projectPath: "/tmp/sandbox-unity",
            timeoutMilliseconds: 8888,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStopExecutionOutput>(result.Output);
        Assert.Equal(DaemonStopStatus.Stopped, output.StopStatus);
        Assert.Equal(DaemonStatusKind.NotRunning, output.DaemonStatus);

        DaemonStopServiceAssert.SupervisorStopCompletedWithoutDirectFallback(
            supervisorProjectGateway,
            daemonStopOperation,
            context);
    }

    private static DaemonStopService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        RecordingDaemonProjectLifecycleGateway supervisorProjectGateway,
        IDaemonStopOperation daemonStopOperation,
        TimeProvider? timeProvider = null)
    {
        return new DaemonStopService(
            resolver,
            supervisorProjectGateway,
            daemonStopOperation,
            timeProvider ?? TimeProvider.System);
    }
}

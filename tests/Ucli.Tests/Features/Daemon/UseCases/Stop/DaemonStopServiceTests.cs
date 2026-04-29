using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
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
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonStopServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenSupervisorIsUnavailable_FallsBackToDirectStopOperation ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope("stop-fallback-not-running");
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 3456,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            TryStopProjectResult = null,
        };
        var service = CreateService(resolver, supervisorProjectGateway, daemonStopOperation);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStopExecutionOutput>(result.Output);
        Assert.Equal("notRunning", output.StopStatus);
        Assert.Equal("notRunning", output.DaemonStatus);
        Assert.Equal(3456, output.TimeoutMilliseconds);
        Assert.Null(output.Session);
        Assert.Equal(1, daemonStopOperation.StopCallCount);
        Assert.Equal(1, supervisorProjectGateway.TryStopProjectCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenExecutionContextResolutionFails_ReturnsFailureWithoutStopCall ()
    {
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(
                ExecutionError.InvalidArgument("invalid project path")));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway();
        var service = CreateService(resolver, supervisorProjectGateway, daemonStopOperation);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(0, daemonStopOperation.StopCallCount);
        Assert.Equal(0, supervisorProjectGateway.TryStopProjectCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenDirectStopOperationFailsWithoutError_ReturnsFallbackInternalError ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope("stop-fallback-failure");
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1100,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = new DaemonStopResult(DaemonStopStatus.Failed, null),
        };
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            TryStopProjectResult = null,
        };
        var service = CreateService(resolver, supervisorProjectGateway, daemonStopOperation);

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
    public async Task Stop_WhenSupervisorGatewayFails_ReturnsFailureWithoutDirectFallback ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 2100,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            TryStopProjectResult = DaemonStopResult.Failure(ExecutionError.InvalidArgument("invalid manifest")),
        };
        var service = CreateService(resolver, supervisorProjectGateway, daemonStopOperation);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(0, daemonStopOperation.StopCallCount);
        Assert.Equal(1, supervisorProjectGateway.TryStopProjectCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenSupervisorProbeConsumesBudget_PropagatesRemainingTimeoutToStopProject ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope("stop-remaining-timeout");
        var timeProvider = new ManualTimeProvider();
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 700,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            TryStopProjectHandler = (unityProject, timeout, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                return ValueTask.FromResult<DaemonStopResult?>(DaemonStopResult.Stopped());
            },
        };
        var service = CreateService(resolver, supervisorProjectGateway, daemonStopOperation, timeProvider: timeProvider);

        var result = await service.Stop(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, daemonStopOperation.StopCallCount);
        Assert.Equal(1, supervisorProjectGateway.TryStopProjectCallCount);
        Assert.Equal(context.Context.UnityProject, supervisorProjectGateway.LastTryStopProjectUnityProject);
        Assert.True(supervisorProjectGateway.LastTryStopProjectTimeout > TimeSpan.Zero);
        Assert.True(supervisorProjectGateway.LastTryStopProjectTimeout <= context.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenSupervisorIsReachable_UsesSupervisorStopProject ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope("stop-supervisor");
        var manifest = DaemonServiceTestContext.CreateSupervisorManifest(scope.FullPath);
        await DaemonServiceTestContext.WriteSupervisorManifest(scope.FullPath, manifest);

        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 2100,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStopOperation = new DaemonServiceTestContext.StubDaemonStopOperation
        {
            StopResult = DaemonStopResult.NotRunning(),
        };
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            TryStopProjectResult = DaemonStopResult.Stopped(),
        };
        var service = CreateService(resolver, supervisorProjectGateway, daemonStopOperation);

        var result = await service.Stop(
            projectPath: "/tmp/sandbox-unity",
            timeout: "8888",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStopExecutionOutput>(result.Output);
        Assert.Equal("stopped", output.StopStatus);
        Assert.Equal("notRunning", output.DaemonStatus);
        Assert.Equal(0, daemonStopOperation.StopCallCount);

        Assert.Equal(1, supervisorProjectGateway.TryStopProjectCallCount);
        Assert.Equal(context.Context.UnityProject, supervisorProjectGateway.LastTryStopProjectUnityProject);
        Assert.True(supervisorProjectGateway.LastTryStopProjectTimeout > TimeSpan.Zero);
        Assert.True(supervisorProjectGateway.LastTryStopProjectTimeout <= context.Timeout);
    }

    private static DaemonStopService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        DaemonServiceTestContext.StubSupervisorProjectGateway supervisorProjectGateway,
        IDaemonStopOperation daemonStopOperation,
        TimeProvider? timeProvider = null)
    {
        return new DaemonStopService(
            resolver,
            supervisorProjectGateway,
            daemonStopOperation,
            timeProvider);
    }
}

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartServiceTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsStarted_ReturnsRunningOutputWithMappedSession ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1200,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper
        {
            Output = DaemonServiceTestContext.CreateSessionOutput(),
        };
        var session = DaemonServiceTestContext.CreateSession();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.Started(session),
        };
        var service = CreateService(resolver, supervisorProjectGateway, mapper);

        var result = await service.Start(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStartExecutionOutput>(result.Output);
        Assert.Equal(DaemonStartStatus.Started, output.StartStatus);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(1200, output.TimeoutMilliseconds);
        Assert.Equal(mapper.Output, output.Session);
        Assert.Equal(1, supervisorProjectGateway.EnsureRunningCallCount);
        Assert.Equal(context.Context.UnityProject, supervisorProjectGateway.LastEnsureRunningUnityProject);
        Assert.True(supervisorProjectGateway.LastEnsureRunningTimeout > TimeSpan.Zero);
        Assert.True(supervisorProjectGateway.LastEnsureRunningTimeout <= context.Timeout);
        Assert.Equal(1, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExecutionContextResolutionFails_ReturnsFailureWithoutSupervisorCall ()
    {
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(
                ExecutionError.InvalidArgument("invalid project path")));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway();
        var service = CreateService(resolver, supervisorProjectGateway, mapper);

        var result = await service.Start(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(0, supervisorProjectGateway.EnsureRunningCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsAlreadyRunning_PreservesAlreadyRunningStatus ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1200,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.AlreadyRunning(DaemonServiceTestContext.CreateSession()),
        };
        var service = CreateService(resolver, supervisorProjectGateway, mapper);

        var result = await service.Start(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStartExecutionOutput>(result.Output);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, output.StartStatus);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsFailure_ReturnsFailure ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1600,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.Failure(ExecutionError.Timeout("start failed")),
        };
        var service = CreateService(resolver, supervisorProjectGateway, mapper);

        var result = await service.Start(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal("start failed", error.Message);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenPluginVerificationConsumesBudget_PropagatesRemainingTimeoutToEnsureRunning ()
    {
        var timeProvider = new ManualTimeProvider();

        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 700,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.Started(DaemonServiceTestContext.CreateSession()),
        };
        var pluginVerifier = new StubUnityPluginVerifier
        {
            Handler = cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                return ValueTask.FromResult(UnityPluginVerificationResult.Success());
            },
        };
        var service = CreateService(resolver, supervisorProjectGateway, mapper, pluginVerifier, timeProvider);

        var result = await service.Start(
            projectPath: "/tmp/sandbox-unity",
            timeoutMilliseconds: 700,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UcliCommandIds.DaemonStart, resolver.LastTimeoutCommand);
        Assert.Equal("/tmp/sandbox-unity", resolver.LastProjectPath);
        Assert.Equal(700, resolver.LastTimeoutMilliseconds);

        Assert.Equal(1, supervisorProjectGateway.EnsureRunningCallCount);
        Assert.Equal(TimeSpan.FromMilliseconds(500), supervisorProjectGateway.LastEnsureRunningTimeout);
        Assert.Equal(context.Context.UnityProject, supervisorProjectGateway.LastEnsureRunningUnityProject);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenUnityPluginMarkerIsMissing_ReturnsInvalidArgumentBeforeSupervisorBootstrap ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1200);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var pluginVerifier = new StubUnityPluginVerifier
        {
            Result = UnityPluginVerificationResult.Failure(ExecutionError.InvalidArgument(
                "Unity project does not contain the uCLI Unity plugin.")),
        };
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway();
        var service = CreateService(resolver, supervisorProjectGateway, mapper, pluginVerifier);

        var result = await service.Start(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(1, pluginVerifier.CallCount);
        Assert.Equal(0, supervisorProjectGateway.EnsureRunningCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenUnityPluginVerificationExceedsTimeout_ReturnsTimeoutBeforeSupervisorBootstrap ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 120);
        var timeProvider = new ManualTimeProvider();
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway();
        var pluginVerifier = new StubUnityPluginVerifier
        {
            Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            Handler = async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return UnityPluginVerificationResult.Success();
            },
        };
        var service = CreateService(resolver, supervisorProjectGateway, mapper, pluginVerifier, timeProvider);

        var resultTask = service.Start(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(pluginVerifier.Started!.Task, "Unity plugin verification start", SignalWaitTimeout);
        timeProvider.Advance(context.Timeout);

        var result = await TestAwaiter.WaitAsync(resultTask, "Unity plugin verification timeout result", SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.True(pluginVerifier.ObservedCancellation);
        Assert.Equal(0, supervisorProjectGateway.EnsureRunningCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    private static DaemonStartService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        DaemonServiceTestContext.StubSupervisorProjectGateway supervisorProjectGateway,
        IDaemonSessionOutputMapper mapper,
        IUnityPluginVerifier? pluginVerifier = null,
        TimeProvider? timeProvider = null)
    {
        pluginVerifier ??= new StubUnityPluginVerifier();
        return new DaemonStartService(resolver, supervisorProjectGateway, pluginVerifier, mapper, timeProvider);
    }

    private sealed class StubUnityPluginVerifier : IUnityPluginVerifier
    {
        public int CallCount { get; private set; }

        public Func<CancellationToken, ValueTask<UnityPluginVerificationResult>>? Handler { get; set; }

        public bool ObservedCancellation { get; private set; }

        public TaskCompletionSource? Started { get; set; }

        public UnityPluginVerificationResult Result { get; set; } = UnityPluginVerificationResult.Success();

        public string? LastUnityProjectRoot { get; private set; }

        public ValueTask<UnityPluginVerificationResult> Verify (
            string unityProjectRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastUnityProjectRoot = unityProjectRoot;
            if (Handler == null)
            {
                return ValueTask.FromResult(Result);
            }

            return LocateCore(cancellationToken);
        }

        private async ValueTask<UnityPluginVerificationResult> LocateCore (CancellationToken cancellationToken)
        {
            try
            {
                Started?.TrySetResult();
                return await Handler!(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                ObservedCancellation = true;
                throw;
            }
        }
    }
}

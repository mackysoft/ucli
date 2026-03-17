using MackySoft.Tests;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.Supervisor;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonStartCommandServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsStarted_ReturnsRunningOutputWithMappedSession ()
    {
        using var scope = DaemonCommandServiceTestContext.CreateTempScope("start-started");
        var manifest = DaemonCommandServiceTestContext.CreateSupervisorManifest(scope.FullPath);
        await DaemonCommandServiceTestContext.WriteSupervisorManifest(scope.FullPath, manifest);

        var context = DaemonCommandServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1200,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper
        {
            Output = DaemonCommandServiceTestContext.CreateSessionOutput(),
        };
        var session = DaemonCommandServiceTestContext.CreateSession();
        var transportClient = CreateTransportClient(
            manifest,
            ensureRunningResponseFactory: request => DaemonCommandServiceTestContext.CreateSuccessResponse(
                request,
                new SupervisorIpcContracts.EnsureRunningResponse(
                    StartStatus: DaemonStartStateCodec.Started,
                    DaemonStatus: DaemonStatusStateCodec.Running,
                    Session: session)));
        var service = CreateService(resolver, transportClient, mapper);

        var result = await service.Start(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStartExecutionOutput>(result.Output);
        Assert.Equal("started", output.StartStatus);
        Assert.Equal("running", output.DaemonStatus);
        Assert.Equal(1200, output.TimeoutMilliseconds);
        Assert.Equal(mapper.Output, output.Session);
        Assert.Equal(2, transportClient.Calls.Count);
        Assert.Equal(1, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExecutionContextResolutionFails_ReturnsFailureWithoutSupervisorCall ()
    {
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(
                ExecutionError.InvalidArgument("invalid project path")));
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var transportClient = new DaemonCommandServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called."),
        };
        var service = CreateService(resolver, transportClient, mapper);

        var result = await service.Start(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Empty(transportClient.Calls);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsAlreadyRunning_PreservesAlreadyRunningStatus ()
    {
        using var scope = DaemonCommandServiceTestContext.CreateTempScope("start-already-running");
        var manifest = DaemonCommandServiceTestContext.CreateSupervisorManifest(scope.FullPath);
        await DaemonCommandServiceTestContext.WriteSupervisorManifest(scope.FullPath, manifest);

        var context = DaemonCommandServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1200,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var transportClient = CreateTransportClient(
            manifest,
            ensureRunningResponseFactory: request => DaemonCommandServiceTestContext.CreateSuccessResponse(
                request,
                new SupervisorIpcContracts.EnsureRunningResponse(
                    StartStatus: DaemonStartStateCodec.AlreadyRunning,
                    DaemonStatus: DaemonStatusStateCodec.Running,
                    Session: DaemonCommandServiceTestContext.CreateSession())));
        var service = CreateService(resolver, transportClient, mapper);

        var result = await service.Start(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStartExecutionOutput>(result.Output);
        Assert.Equal("alreadyRunning", output.StartStatus);
        Assert.Equal("running", output.DaemonStatus);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsFailure_ReturnsFailure ()
    {
        using var scope = DaemonCommandServiceTestContext.CreateTempScope("start-failure");
        var manifest = DaemonCommandServiceTestContext.CreateSupervisorManifest(scope.FullPath);
        await DaemonCommandServiceTestContext.WriteSupervisorManifest(scope.FullPath, manifest);

        var context = DaemonCommandServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1600,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var transportClient = CreateTransportClient(
            manifest,
            ensureRunningResponseFactory: request => DaemonCommandServiceTestContext.CreateErrorResponse(
                request,
                CliErrorCodes.IpcTimeout,
                "start failed"));
        var service = CreateService(resolver, transportClient, mapper);

        var result = await service.Start(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal("start failed", error.Message);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenBootstrapConsumesBudget_PropagatesRemainingTimeoutToEnsureRunning ()
    {
        using var scope = DaemonCommandServiceTestContext.CreateTempScope("start-remaining-timeout");
        var manifest = DaemonCommandServiceTestContext.CreateSupervisorManifest(scope.FullPath);
        await DaemonCommandServiceTestContext.WriteSupervisorManifest(scope.FullPath, manifest);
        var timeProvider = new ManualTimeProvider();

        var context = DaemonCommandServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 700,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var transportClient = CreateTransportClient(
            manifest,
            timeProvider: timeProvider,
            pingDelay: TimeSpan.FromMilliseconds(200),
            ensureRunningResponseFactory: request => DaemonCommandServiceTestContext.CreateSuccessResponse(
                request,
                new SupervisorIpcContracts.EnsureRunningResponse(
                    StartStatus: DaemonStartStateCodec.Started,
                    DaemonStatus: DaemonStatusStateCodec.Running,
                    Session: DaemonCommandServiceTestContext.CreateSession())));
        var service = CreateService(resolver, transportClient, mapper, timeProvider: timeProvider);

        var result = await service.Start(
            projectPath: "/tmp/sandbox-unity",
            timeout: "700",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UcliCommandIds.DaemonStart, resolver.LastTimeoutCommand);
        Assert.Equal("/tmp/sandbox-unity", resolver.LastProjectPath);
        Assert.Equal("700", resolver.LastTimeoutOption);

        var ensureRunningCall = Assert.Single(
            transportClient.Calls,
            static x => x.Request.Method == SupervisorIpcContracts.EnsureRunningMethod);
        Assert.True(ensureRunningCall.Timeout < context.Timeout);

        Assert.True(IpcPayloadCodec.TryDeserialize(
            ensureRunningCall.Request.Payload,
            out SupervisorIpcContracts.EnsureRunningRequest payload,
            out _));
        Assert.Equal(context.Context.UnityProject.UnityProjectRoot, payload.UnityProjectRoot);
        Assert.Equal(context.Context.UnityProject.ProjectFingerprint, payload.ProjectFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenInitialPingTimesOutAndRetrySucceeds_ReusesExistingSupervisorManifest ()
    {
        using var scope = DaemonCommandServiceTestContext.CreateTempScope("start-ping-timeout-retry");
        var manifest = DaemonCommandServiceTestContext.CreateSupervisorManifest(scope.FullPath);
        await DaemonCommandServiceTestContext.WriteSupervisorManifest(scope.FullPath, manifest);

        var context = DaemonCommandServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1600,
            repositoryRoot: scope.FullPath);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var pingAttemptCount = 0;
        var transportClient = new DaemonCommandServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (endpoint, request, _, _) =>
            {
                Assert.Equal(manifest.EndpointAddress, endpoint.Address);
                Assert.Equal(manifest.SessionToken, request.SessionToken);
                if (request.Method == SupervisorIpcContracts.PingMethod)
                {
                    pingAttemptCount++;
                    if (pingAttemptCount == 1)
                    {
                        throw new TimeoutException("Supervisor ping timed out.");
                    }

                    return ValueTask.FromResult(DaemonCommandServiceTestContext.CreateSuccessResponse(
                        request,
                        new SupervisorIpcContracts.PingResponse(manifest.ProcessId, manifest.IssuedAtUtc)));
                }

                if (request.Method == SupervisorIpcContracts.EnsureRunningMethod)
                {
                    return ValueTask.FromResult(DaemonCommandServiceTestContext.CreateSuccessResponse(
                        request,
                        new SupervisorIpcContracts.EnsureRunningResponse(
                            StartStatus: DaemonStartStateCodec.Started,
                            DaemonStatus: DaemonStatusStateCodec.Running,
                            Session: DaemonCommandServiceTestContext.CreateSession())));
                }

                throw new InvalidOperationException($"Unexpected supervisor IPC method: {request.Method}");
            },
        };
        var service = CreateService(resolver, transportClient, mapper);

        var result = await service.Start(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, pingAttemptCount);
        Assert.Single(
            transportClient.Calls,
            static x => x.Request.Method == SupervisorIpcContracts.EnsureRunningMethod);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenUnityPluginMarkerIsMissing_ReturnsInvalidArgumentBeforeSupervisorBootstrap ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1200);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var transportClient = new DaemonCommandServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new Xunit.Sdk.XunitException("Supervisor transport must not be called."),
        };
        var pluginLocator = new StubUnityUcliPluginLocator
        {
            Result = UnityUcliPluginLocateResult.NotFound(ExecutionError.InvalidArgument(
                "Unity project does not contain the uCLI Unity plugin.")),
        };
        var service = CreateService(resolver, transportClient, mapper, pluginLocator);

        var result = await service.Start(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(1, pluginLocator.CallCount);
        Assert.Empty(transportClient.Calls);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenUnityPluginVerificationExceedsTimeout_ReturnsTimeoutBeforeSupervisorBootstrap ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 120);
        var timeProvider = new ManualTimeProvider();
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var transportClient = new DaemonCommandServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new Xunit.Sdk.XunitException("Supervisor transport must not be called."),
        };
        var pluginLocator = new StubUnityUcliPluginLocator
        {
            Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            Handler = async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return UnityUcliPluginLocateResult.Found(
                    "/tmp/ucli-plugin.json",
                    UnityUcliPluginLocator.ExpectedProtocolVersion);
            },
        };
        var service = CreateService(resolver, transportClient, mapper, pluginLocator, timeProvider);

        var resultTask = service.Start(projectPath: null, timeout: null, cancellationToken: CancellationToken.None).AsTask();
        await pluginLocator.Started!.Task;
        timeProvider.Advance(context.Timeout);

        var result = await resultTask;

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.True(pluginLocator.ObservedCancellation);
        Assert.Empty(transportClient.Calls);
        Assert.Equal(0, mapper.CallCount);
    }

    private static DaemonStartCommandService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        DaemonCommandServiceTestContext.StubIpcTransportClient transportClient,
        IDaemonSessionOutputMapper mapper,
        IUnityUcliPluginLocator? pluginLocator = null,
        TimeProvider? timeProvider = null)
    {
        var bootstrapper = DaemonCommandServiceTestContext.CreateSupervisorBootstrapper(transportClient, timeProvider: timeProvider);
        var supervisorClient = DaemonCommandServiceTestContext.CreateSupervisorClient(transportClient);
        pluginLocator ??= new StubUnityUcliPluginLocator();
        return new DaemonStartCommandService(resolver, bootstrapper, supervisorClient, pluginLocator, mapper, timeProvider);
    }

    private static DaemonCommandServiceTestContext.StubIpcTransportClient CreateTransportClient (
        SupervisorInstanceManifest manifest,
        Func<IpcRequest, IpcResponse> ensureRunningResponseFactory,
        TimeProvider? timeProvider = null,
        TimeSpan? pingDelay = null)
    {
        return new DaemonCommandServiceTestContext.StubIpcTransportClient
        {
            SendHandler = async (endpoint, request, timeout, cancellationToken) =>
            {
                Assert.Equal(manifest.EndpointAddress, endpoint.Address);
                if (request.Method == SupervisorIpcContracts.PingMethod)
                {
                    if (pingDelay.HasValue)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (timeProvider is ManualTimeProvider manualTimeProvider)
                        {
                            manualTimeProvider.Advance(pingDelay.Value);
                        }
                        else
                        {
                            throw new InvalidOperationException("ManualTimeProvider is required when pingDelay is configured.");
                        }
                    }

                    return DaemonCommandServiceTestContext.CreateSuccessResponse(
                        request,
                        new SupervisorIpcContracts.PingResponse(manifest.ProcessId, manifest.IssuedAtUtc));
                }

                if (request.Method == SupervisorIpcContracts.EnsureRunningMethod)
                {
                    return ensureRunningResponseFactory(request);
                }

                throw new InvalidOperationException($"Unexpected supervisor IPC method: {request.Method}");
            },
        };
    }

    private sealed class StubUnityUcliPluginLocator : IUnityUcliPluginLocator
    {
        public int CallCount { get; private set; }

        public Func<CancellationToken, ValueTask<UnityUcliPluginLocateResult>>? Handler { get; set; }

        public bool ObservedCancellation { get; private set; }

        public TaskCompletionSource? Started { get; set; }

        public UnityUcliPluginLocateResult Result { get; set; }
            = UnityUcliPluginLocateResult.Found(
                "/tmp/ucli-plugin.json",
                UnityUcliPluginLocator.ExpectedProtocolVersion);

        public string? LastUnityProjectRoot { get; private set; }

        public ValueTask<UnityUcliPluginLocateResult> Locate (
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

        private async ValueTask<UnityUcliPluginLocateResult> LocateCore (CancellationToken cancellationToken)
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
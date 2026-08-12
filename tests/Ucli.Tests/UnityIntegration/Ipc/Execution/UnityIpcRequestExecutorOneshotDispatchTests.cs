using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.Tests.Helpers.Unity;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using static MackySoft.Ucli.Tests.Ipc.UnityIpcRequestExecutorTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcRequestExecutorOneshotDispatchTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task BindResolvedTargetAsync_WhenTargetIsOneshot_VerifiesPluginWithoutDecidingMode ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "resolved-oneshot-bind");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(request =>
        {
            Assert.Equal(UnityIpcMethod.Ping, IpcRequestAssert.ParseMethod(request));
            return CreateReadyPingResponse(request.RequestId, unityProject.ProjectFingerprint);
        });
        var modeDecisionService = new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
            new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                false,
                UnityExecutionTarget.Oneshot,
                DefaultTimeout)));
        var pluginLocator = new RecordingUnityUcliPluginLocator();
        var executor = CreateExecutor(
            modeDecisionService,
            new RecordingDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(
                new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called.")),
                oneshotTransportClient,
                new UnexpectedDaemonSessionStore("Resolved oneshot binding should not resolve a daemon session."),
                new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle))));
        var deadline = ExecutionDeadline.Start(DefaultTimeout, TimeProvider.System);

        var binding = await executor.BindResolvedTargetAsync(
            unityProject,
            UnityExecutionTarget.Oneshot,
            deadline);

        Assert.True(binding.IsSuccess);
        Assert.Empty(modeDecisionService.Invocations);
        UnityPluginLocatorAssert.VerificationAttemptedFor(pluginLocator, unityProject);
        await binding.Binding!.DisposeAsync();
        Assert.Equal(1, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Bind_WhenTargetIsOneshot_LaunchesOneLeaseAndDisposalCleansItWithoutResolvingAnotherHost ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "oneshot-bind");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle();
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(request =>
        {
            Assert.Equal(UnityIpcMethod.Ping, IpcRequestAssert.ParseMethod(request));
            return CreateReadyPingResponse(request.RequestId, unityProject.ProjectFingerprint);
        });
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var modeDecisionService = new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Oneshot,
                    false,
                    UnityExecutionTarget.Oneshot,
                    DefaultTimeout)));
        var pluginLocator = new RecordingUnityUcliPluginLocator();
        var executor = CreateExecutor(
            modeDecisionService,
            new RecordingDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(
                new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called.")),
                oneshotTransportClient,
                new UnexpectedDaemonSessionStore("Oneshot binding should not resolve a daemon session."),
                launcher));

        var binding = await executor.BindAsync(
            UnityExecutionMode.Oneshot,
            unityProject,
            ExecutionDeadline.Start(DefaultTimeout, TimeProvider.System));

        Assert.True(binding.IsSuccess);
        Assert.Single(modeDecisionService.Invocations);
        UnityPluginLocatorAssert.VerificationAttemptedFor(pluginLocator, unityProject);
        UnityOneshotLaunchAssert.LaunchedOnce(launcher, unityProject);

        await binding.Binding!.DisposeAsync();

        Assert.Equal(1, processHandle.DisposeCount);
        Assert.Single(oneshotTransportClient.Requests);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenTargetIsOneshot_UsesOneshotClient ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "oneshot");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var response = CreateSuccessResponse(Guid.NewGuid());
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(request =>
        {
            return IpcRequestAssert.ParseMethod(request) switch
            {
                UnityIpcMethod.Ping => CreateReadyPingResponse(request.RequestId, unityProject.ProjectFingerprint),
                UnityIpcMethod.OpsRead => response,
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    false,
                    UnityExecutionTarget.Oneshot,
                    DefaultTimeout))),
            new RecordingDaemonPingInfoClient(),
            new RecordingUnityUcliPluginLocator(),
            CreateClients(
                daemonTransportClient,
                oneshotTransportClient,
                new UnexpectedDaemonSessionStore("Oneshot target should not resolve a daemon session."),
                launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            unityProject,
            CreateOpsReadPayload());

        Assert.True(result.IsSuccess);
        AssertSuccessfulUnityResponse(response, result.Response);
        UnityIpcExecutionPathAssert.OneshotExecutionStartedOnly(
            daemonTransportClient,
            oneshotTransportClient,
            launcher,
            UnityIpcMethod.Ping,
            UnityIpcMethod.OpsRead);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenUnityPluginMarkerIsMissing_ReturnsInvalidArgumentWithoutCallingClients ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "plugin-missing");
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var pluginLocator = new RecordingUnityUcliPluginLocator
        {
            Result = UnityUcliPluginLocateResult.NotFound(ExecutionError.InvalidArgument(
                "Unity project does not contain the uCLI Unity plugin.")),
        };
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    false,
                    UnityExecutionTarget.Oneshot,
                    DefaultTimeout))),
            new RecordingDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(
                daemonTransportClient,
                oneshotTransportClient,
                new UnexpectedDaemonSessionStore("Plugin verification failure should not resolve a daemon session."),
                launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Oneshot,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            unityProject,
            CreateOpsReadPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        UnityPluginLocatorAssert.VerificationAttemptedFor(pluginLocator, unityProject);
        UnityIpcExecutionPathAssert.NoUnityExecutionWasStarted(
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenUnityPluginVerificationExceedsTimeout_ReturnsTimeoutWithoutCallingClients ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "plugin-timeout");
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var pluginLocator = new RecordingUnityUcliPluginLocator
        {
            Handler = async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return UnityUcliPluginLocateResult.Found(
                    AbsolutePath.Resolve(
                        AbsolutePath.Parse(Environment.CurrentDirectory),
                        "ucli-plugin.json"),
                    UnityUcliPluginMarkerContract.ExpectedProtocolVersion);
            },
        };
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    false,
                    UnityExecutionTarget.Oneshot,
                    TimeSpan.FromMilliseconds(120)))),
            new RecordingDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(
                daemonTransportClient,
                oneshotTransportClient,
                new UnexpectedDaemonSessionStore("Plugin timeout should not resolve a daemon session."),
                launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromMilliseconds(120),
            UcliConfig.CreateDefault(),
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateOpsReadPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.True(pluginLocator.ObservedCancellation);
        UnityIpcExecutionPathAssert.NoUnityExecutionWasStarted(
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }
}

using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
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

public sealed class UnityIpcRequestExecutorDaemonDispatchTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenExplicitDaemonModeIsRequested_DispatchesWithoutReachabilityProbe ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "explicit-daemon");
        var response = CreateSuccessResponse(Guid.NewGuid());
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => response);
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token"));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var modeDecisionService = new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
            new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                false,
                UnityExecutionTarget.Oneshot,
                DefaultTimeout)))
        {
            OnDecide = static _ => throw new Xunit.Sdk.XunitException("Explicit daemon dispatch must not probe reachability."),
        };
        var executor = CreateExecutor(
            modeDecisionService,
            new RecordingDaemonPingInfoClient(),
            new RecordingUnityUcliPluginLocator(),
            CreateClients(daemonTransportClient, oneshotTransportClient, sessionConnectionProvider, launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Daemon,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateOpsReadPayload());

        Assert.True(result.IsSuccess);
        AssertSuccessfulUnityResponse(response, result.Response);
        UnityIpcExecutionPathAssert.ExplicitDaemonEndpointDispatchedWithoutModeDecision(
            daemonTransportClient,
            oneshotTransportClient,
            launcher,
            modeDecisionService,
            "/tmp/ucli-session.sock");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenTargetIsDaemon_UsesDaemonClient ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "daemon");
        var response = CreateSuccessResponse(Guid.NewGuid());
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => response);
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token"));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var pluginLocator = new RecordingUnityUcliPluginLocator
        {
            Result = UnityUcliPluginLocateResult.NotFound(ExecutionError.InvalidArgument(
                "Unity project does not contain the uCLI Unity plugin.")),
        };
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout))),
            new RecordingDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(daemonTransportClient, oneshotTransportClient, sessionConnectionProvider, launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateOpsReadPayload());

        Assert.True(result.IsSuccess);
        AssertSuccessfulUnityResponse(response, result.Response);
        var request = UnityIpcExecutionPathAssert.DaemonRequestDispatchedOnlyWithoutPluginVerification(
            pluginLocator,
            daemonTransportClient,
            oneshotTransportClient,
            launcher,
            IpcMethodNames.OpsRead);
        Assert.Equal("daemon-token", request.SessionToken);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ExecuteStreaming_WhenTargetIsDaemon_SendsStreamResponseModeAndForwardsProgress ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "daemon-stream");
        var response = CreateSuccessResponse(Guid.NewGuid());
        var daemonTransportClient = new RecordingUnityIpcTransportClient(
            _ => response,
            request => new IpcStreamFrame(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcStreamFrameKinds.Progress,
                "ops.progress",
                EmptyPayload(),
                response: null));
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token"));
        var progressFrames = new List<UnityRequestProgressFrame>();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout))),
            new RecordingDaemonPingInfoClient(),
            new RecordingUnityUcliPluginLocator(),
            CreateClients(daemonTransportClient, oneshotTransportClient, sessionConnectionProvider, launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateOpsReadPayload(),
            (frame, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progressFrames.Add(frame);
                return ValueTask.CompletedTask;
            });

        Assert.True(result.IsSuccess);
        AssertSuccessfulUnityResponse(response, result.Response);
        var request = UnityIpcExecutionPathAssert.DaemonStreamingRequestDispatchedOnly(
            daemonTransportClient,
            oneshotTransportClient,
            launcher,
            IpcMethodNames.OpsRead);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Stream), request.ResponseMode);
        var progressFrame = Assert.Single(progressFrames);
        Assert.Equal("ops.progress", progressFrame.Event);
        Assert.Equal(JsonValueKind.Object, progressFrame.Payload.ValueKind);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenDaemonModeSessionIsMissing_ReturnsDaemonNotRunningWithoutPluginVerification ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "daemon-plugin-missing");
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var pluginLocator = new RecordingUnityUcliPluginLocator
        {
            Result = UnityUcliPluginLocateResult.NotFound(ExecutionError.InvalidArgument(
                "Unity project does not contain the uCLI Unity plugin.")),
        };
        var executor = CreateExecutor(
            new StubModeDecisionService(
                UnityExecutionModeDecisionResult.ContractFailure(
                    new UnityExecutionModeDecisionContractError(
                        UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                        "Daemon is not running for mode=daemon."))),
            new RecordingDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(
                daemonTransportClient,
                oneshotTransportClient,
                new QueuedDaemonSessionConnectionProvider(DaemonSessionConnectionResolutionResult.SessionNotAvailable()),
                launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Daemon,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateOpsReadPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        UnityIpcExecutionPathAssert.NoPluginVerificationOrUnityExecutionWasStarted(
            pluginLocator,
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }
}

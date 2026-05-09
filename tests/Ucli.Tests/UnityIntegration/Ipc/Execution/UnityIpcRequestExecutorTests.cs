using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.TestDoubles;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using MackySoft.Ucli.UnityIntegration.Project.Plugin;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcRequestExecutorTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenModeDecisionReturnsContractError_ReturnsContractFailureWithoutCallingClients ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "contract-error");
        var daemonTransportClient = new StubUnityIpcTransportClient(_ => CreateResponse("unused"));
        var oneshotTransportClient = new StubUnityIpcTransportClient(_ => CreateResponse("unused"));
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var executor = CreateExecutor(
            new StubModeDecisionService(
                UnityExecutionModeDecisionResult.ContractFailure(
                    new UnityExecutionModeDecisionContractError(
                        UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                        "Daemon is not running for mode=daemon."))),
            new StubDaemonPingInfoClient(),
            new StubUnityUcliPluginLocator(),
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Daemon,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            new UnityRequestPayload.Raw(
                IpcMethodNames.OpsRead,
                EmptyPayload()));

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        Assert.Equal(0, daemonTransportClient.CallCount);
        Assert.Equal(0, oneshotTransportClient.CallCount);
        Assert.Equal(0, launcher.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenModeDecisionThrows_ReturnsInternalErrorWithoutCallingClients ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "mode-decision-exception");
        var daemonTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var pluginLocator = new StubUnityUcliPluginLocator();
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout)))
            {
                OnDecide = static _ => throw new InvalidOperationException("mode decision failed"),
            },
            new StubDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            new UnityRequestPayload.Raw(
                IpcMethodNames.OpsRead,
                EmptyPayload()));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("Failed to decide Unity execution mode.", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, pluginLocator.CallCount);
        Assert.Equal(0, daemonTransportClient.CallCount);
        Assert.Equal(0, oneshotTransportClient.CallCount);
        Assert.Equal(0, launcher.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTargetIsDaemon_UsesDaemonClient ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "daemon");
        var response = CreateResponse("req-daemon");
        var daemonTransportClient = new StubUnityIpcTransportClient(_ => response);
        var oneshotTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider
        {
            Result = DaemonSessionTokenResolutionResult.Success("daemon-token"),
        };
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var pluginLocator = new StubUnityUcliPluginLocator
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
            new StubDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(daemonTransportClient, oneshotTransportClient, sessionTokenProvider, launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            new UnityRequestPayload.Raw(
                IpcMethodNames.OpsRead,
                EmptyPayload()));

        Assert.True(result.IsSuccess);
        AssertUnityResponse(response, result.Response);
        Assert.Equal(1, daemonTransportClient.CallCount);
        Assert.Equal(0, oneshotTransportClient.CallCount);
        Assert.Equal(1, sessionTokenProvider.CallCount);
        Assert.Equal("daemon-token", daemonTransportClient.Requests[0].SessionToken);
        Assert.Equal(0, pluginLocator.CallCount);
        Assert.Equal(0, launcher.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonOpsReadRequiresReadinessGate_ConvertsDispatchToFailFastGate ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "daemon-ops-readiness");
        var response = CreateResponse("req-daemon-readiness");
        var daemonTransportClient = new StubUnityIpcTransportClient(_ => response);
        var oneshotTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider
        {
            Result = DaemonSessionTokenResolutionResult.Success("daemon-token"),
        };
        var readinessProbe = new StubDaemonPingInfoClient(
            CreatePingPayload(IpcEditorLifecycleStateCodec.Busy, false),
            CreatePingPayload(IpcEditorLifecycleStateCodec.Ready, true));
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout))),
            readinessProbe,
            new StubUnityUcliPluginLocator(),
            CreateClients(daemonTransportClient, oneshotTransportClient, sessionTokenProvider, launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            new UnityRequestPayload.Raw(
                IpcMethodNames.OpsRead,
                IpcPayloadCodec.SerializeToElement(new IpcOpsReadRequest(
                    FailFast: false,
                    RequireReadinessGate: true))));

        Assert.True(result.IsSuccess);
        AssertUnityResponse(response, result.Response);
        Assert.Equal(2, readinessProbe.CallCount);
        Assert.Equal(1, daemonTransportClient.CallCount);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            daemonTransportClient.Requests[0].Payload,
            out IpcOpsReadRequest payload,
            out _));
        Assert.True(payload.RequireReadinessGate);
        Assert.True(payload.FailFast);
        Assert.Equal(0, oneshotTransportClient.CallCount);
        Assert.Equal(0, launcher.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonOpsReadLateBusyRegressionOccurs_RewaitsAndRedispatches ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "daemon-ops-late-busy");
        var responses = new Queue<IpcResponse>(new[]
        {
            CreateErrorResponse(
                "req-daemon-busy",
                EditorLifecycleErrorCodes.EditorBusy,
                "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            CreateResponse("req-daemon-ready"),
        });
        var daemonTransportClient = new StubUnityIpcTransportClient(_ => responses.Dequeue());
        var oneshotTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var sessionTokenProvider = new StubDaemonSessionTokenProvider
        {
            Result = DaemonSessionTokenResolutionResult.Success("daemon-token"),
        };
        var readinessProbe = new StubDaemonPingInfoClient(
            CreatePingPayload(IpcEditorLifecycleStateCodec.Ready, true),
            CreatePingPayload(IpcEditorLifecycleStateCodec.Ready, true));
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout))),
            readinessProbe,
            new StubUnityUcliPluginLocator(),
            CreateClients(daemonTransportClient, oneshotTransportClient, sessionTokenProvider, launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            new UnityRequestPayload.Raw(
                IpcMethodNames.OpsRead,
                IpcPayloadCodec.SerializeToElement(new IpcOpsReadRequest(
                    FailFast: false,
                    RequireReadinessGate: true))));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, readinessProbe.CallCount);
        Assert.Equal(2, daemonTransportClient.CallCount);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            daemonTransportClient.Requests[0].Payload,
            out IpcOpsReadRequest firstPayload,
            out _));
        Assert.True(firstPayload.RequireReadinessGate);
        Assert.True(firstPayload.FailFast);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            daemonTransportClient.Requests[1].Payload,
            out IpcOpsReadRequest secondPayload,
            out _));
        Assert.True(secondPayload.RequireReadinessGate);
        Assert.True(secondPayload.FailFast);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonOpsReadFailFastHitsBusyState_ReturnsLifecycleFailureWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "daemon-ops-fail-fast");
        var daemonTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var readinessProbe = new StubDaemonPingInfoClient(
            CreatePingPayload(IpcEditorLifecycleStateCodec.Busy, false));
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout))),
            readinessProbe,
            new StubUnityUcliPluginLocator(),
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            new UnityRequestPayload.Raw(
                IpcMethodNames.OpsRead,
                IpcPayloadCodec.SerializeToElement(new IpcOpsReadRequest(
                    FailFast: true,
                    RequireReadinessGate: true))));

        Assert.False(result.IsSuccess);
        Assert.Equal(EditorLifecycleErrorCodes.EditorBusy, result.ErrorCode);
        Assert.Contains("Unity editor is busy with internal work.", result.Message, StringComparison.Ordinal);
        Assert.Equal(1, readinessProbe.CallCount);
        Assert.Equal(0, daemonTransportClient.CallCount);
        Assert.Equal(0, oneshotTransportClient.CallCount);
        Assert.Equal(0, launcher.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTargetIsOneshot_UsesOneshotClient ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "oneshot");
        var response = CreateResponse("req-oneshot");
        var daemonTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new StubUnityIpcTransportClient(request =>
        {
            return request.Method switch
            {
                IpcMethodNames.Ping => CreatePingResponse(request.RequestId),
                IpcMethodNames.OpsRead => response,
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    false,
                    UnityExecutionTarget.Oneshot,
                    DefaultTimeout))),
            new StubDaemonPingInfoClient(),
            new StubUnityUcliPluginLocator(),
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            new UnityRequestPayload.Raw(
                IpcMethodNames.OpsRead,
                EmptyPayload()));

        Assert.True(result.IsSuccess);
        AssertUnityResponse(response, result.Response);
        Assert.Equal(0, daemonTransportClient.CallCount);
        Assert.Equal(2, oneshotTransportClient.CallCount);
        Assert.Equal(1, launcher.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityPluginMarkerIsMissing_ReturnsInvalidArgumentWithoutCallingClients ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "plugin-missing");
        var daemonTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var pluginLocator = new StubUnityUcliPluginLocator
        {
            Result = UnityUcliPluginLocateResult.NotFound(ExecutionError.InvalidArgument(
                "Unity project does not contain the uCLI Unity plugin.")),
        };
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    false,
                    UnityExecutionTarget.Oneshot,
                    DefaultTimeout))),
            new StubDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Oneshot,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            new UnityRequestPayload.Raw(
                IpcMethodNames.OpsRead,
                EmptyPayload()));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Equal(1, pluginLocator.CallCount);
        Assert.Equal(0, daemonTransportClient.CallCount);
        Assert.Equal(0, oneshotTransportClient.CallCount);
        Assert.Equal(0, launcher.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonModeReportsNotRunningAndUnityPluginMarkerIsMissing_ReturnsPluginFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "daemon-plugin-missing");
        var daemonTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var pluginLocator = new StubUnityUcliPluginLocator
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
            new StubDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Daemon,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            new UnityRequestPayload.Raw(
                IpcMethodNames.OpsRead,
                EmptyPayload()));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Equal(1, pluginLocator.CallCount);
        Assert.Equal(0, daemonTransportClient.CallCount);
        Assert.Equal(0, oneshotTransportClient.CallCount);
        Assert.Equal(0, launcher.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityPluginVerificationExceedsTimeout_ReturnsTimeoutWithoutCallingClients ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "plugin-timeout");
        var daemonTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var pluginLocator = new StubUnityUcliPluginLocator
        {
            Handler = async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return UnityUcliPluginLocateResult.Found(
                    "/tmp/ucli-plugin.json",
                    UnityUcliPluginLocator.ExpectedProtocolVersion);
            },
        };
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    false,
                    UnityExecutionTarget.Oneshot,
                    TimeSpan.FromMilliseconds(120)))),
            new StubDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromMilliseconds(120),
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            new UnityRequestPayload.Raw(
                IpcMethodNames.OpsRead,
                EmptyPayload()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.True(pluginLocator.ObservedCancellation);
        Assert.Equal(0, daemonTransportClient.CallCount);
        Assert.Equal(0, oneshotTransportClient.CallCount);
        Assert.Equal(0, launcher.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenModeDecisionConsumesSharedBudget_ReturnsTimeoutBeforeDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "mode-decision-budget");
        var timeProvider = new ManualTimeProvider();
        var daemonTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new StubUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var modeDecisionService = new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
            new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                true,
                UnityExecutionTarget.Daemon,
                TimeSpan.FromMilliseconds(100))))
        {
            TimeProvider = timeProvider,
            OnDecide = static context =>
            {
                ((ManualTimeProvider)context.TimeProvider).Advance(TimeSpan.FromMilliseconds(120));
            },
        };
        var executor = CreateExecutor(
            modeDecisionService,
            new StubDaemonPingInfoClient(),
            new StubUnityUcliPluginLocator(),
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher),
            timeProvider);

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(100),
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            new UnityRequestPayload.Raw(
                IpcMethodNames.OpsRead,
                EmptyPayload()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Equal(TimeSpan.FromMilliseconds(100), modeDecisionService.LastTimeout);
        Assert.Equal(0, daemonTransportClient.CallCount);
        Assert.Equal(0, oneshotTransportClient.CallCount);
        Assert.Equal(0, launcher.CallCount);
    }

    private static IUnityIpcClient[] CreateClients (
        StubUnityIpcTransportClient daemonTransportClient,
        StubUnityIpcTransportClient oneshotTransportClient,
        StubDaemonSessionTokenProvider sessionTokenProvider,
        StubUnityBatchmodeProcessLauncher launcher)
    {
        return
        [
            new UnityDaemonIpcClient(daemonTransportClient, sessionTokenProvider),
            new UnityOneshotIpcClient(
                launcher,
                new StubIpcEndpointResolver(new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-oneshot.sock")),
                oneshotTransportClient,
                new StubProjectLifecycleLockProvider(),
                new StubUnityProjectLockFileProbe()),
        ];
    }

    private static UnityIpcRequestExecutor CreateExecutor (
        StubModeDecisionService modeDecisionService,
        StubDaemonPingInfoClient daemonPingInfoClient,
        StubUnityUcliPluginLocator pluginLocator,
        IUnityIpcClient[] clients,
        TimeProvider? timeProvider = null)
    {
        return new UnityIpcRequestExecutor(
            new UnityIpcRequestBuilder(),
            new UnityIpcExecutionTargetResolver(
                modeDecisionService,
                new UnityIpcPluginVerifier(pluginLocator)),
            new UnityIpcClientSelector(clients),
            new UnityDaemonReadinessGate(daemonPingInfoClient, timeProvider),
            timeProvider);
    }

    private static ResolvedUnityProjectContext CreateContext (TestDirectoryScope scope)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: scope.GetPath("UnityProject"),
            RepositoryRoot: scope.FullPath,
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static JsonElement EmptyPayload ()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    private static IpcResponse CreateResponse (string requestId)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: EmptyPayload(),
            Errors: Array.Empty<IpcError>());
    }

    private static void AssertUnityResponse (
        IpcResponse expected,
        UnityRequestResponse? actual)
    {
        Assert.NotNull(actual);
        Assert.False(actual!.HasFailureStatus);
        Assert.Equal(expected.Payload.GetRawText(), actual.Payload.GetRawText());
        Assert.Equal(expected.Errors.Count, actual.Errors.Count);
        for (var i = 0; i < expected.Errors.Count; i++)
        {
            Assert.Equal(expected.Errors[i].Code, actual.Errors[i].Code);
            Assert.Equal(expected.Errors[i].Message, actual.Errors[i].Message);
            Assert.Equal(expected.Errors[i].OpId, actual.Errors[i].OpId);
        }
    }

    private static IpcResponse CreateErrorResponse (
        string requestId,
        UcliErrorCode errorCode,
        string message)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusError,
            Payload: EmptyPayload(),
            Errors:
            [
                new IpcError(errorCode, message, null),
            ]);
    }

    private static IpcResponse CreatePingResponse (string requestId)
    {
        var payload = IpcPayloadCodec.SerializeToElement(CreatePingPayload(
            IpcEditorLifecycleStateCodec.Ready,
            true));
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Status: IpcProtocol.StatusOk,
            Payload: payload,
            Errors: Array.Empty<IpcError>());
    }

    private static IpcPingResponse CreatePingPayload (
        string lifecycleState,
        bool canAcceptExecutionRequests)
    {
        return IpcPingResponseTestFactory.Create(
            lifecycleState: lifecycleState,
            canAcceptExecutionRequests: canAcceptExecutionRequests);
    }

    private sealed class StubModeDecisionService : IUnityExecutionModeDecisionService
    {
        private readonly UnityExecutionModeDecisionResult result;

        public Action<ModeDecisionInvocationContext>? OnDecide { get; init; }

        public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

        public TimeSpan LastTimeout { get; private set; }

        public StubModeDecisionService (UnityExecutionModeDecisionResult result)
        {
            this.result = result;
        }

        public ValueTask<UnityExecutionModeDecisionResult> DecideAsync (
            UnityExecutionMode mode,
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastTimeout = timeout;
            OnDecide?.Invoke(new ModeDecisionInvocationContext(
                mode,
                unityProject,
                timeout,
                cancellationToken,
                TimeProvider));
            return ValueTask.FromResult(result);
        }
    }

    private sealed record ModeDecisionInvocationContext (
        UnityExecutionMode Mode,
        ResolvedUnityProjectContext UnityProject,
        TimeSpan Timeout,
        CancellationToken CancellationToken,
        TimeProvider TimeProvider);

    private sealed class StubUnityIpcTransportClient : IUnityIpcTransportClient
    {
        private readonly Func<IpcRequest, IpcResponse> responseFactory;

        public StubUnityIpcTransportClient (Func<IpcRequest, IpcResponse> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public int CallCount { get; private set; }

        public List<IpcRequest> Requests { get; } = new List<IpcRequest>();

        public ValueTask<IpcResponse> SendAsync (
            string storageRoot,
            string projectFingerprint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Requests.Add(request);
            return ValueTask.FromResult(responseFactory(request));
        }
    }

    private sealed class StubDaemonSessionTokenProvider : IDaemonSessionTokenProvider
    {
        public int CallCount { get; private set; }

        public DaemonSessionTokenResolutionResult Result { get; set; }
            = DaemonSessionTokenResolutionResult.SessionNotAvailable();

        public ValueTask<DaemonSessionTokenResolutionResult> ResolveAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubDaemonPingInfoClient : IDaemonPingInfoClient
    {
        private readonly Queue<IpcPingResponse> responses = new Queue<IpcPingResponse>();

        public StubDaemonPingInfoClient (params IpcPingResponse[] responses)
        {
            foreach (var response in responses)
            {
                this.responses.Enqueue(response);
            }
        }

        public int CallCount { get; private set; }

        public ValueTask<IpcPingResponse> PingAndReadAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            bool validateProjectFingerprint = true,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (responses.Count == 0)
            {
                throw new Xunit.Sdk.XunitException("No daemon ping response was configured.");
            }

            return ValueTask.FromResult(responses.Dequeue());
        }
    }

    private sealed class StubUnityBatchmodeProcessLauncher : IUnityBatchmodeProcessLauncher
    {
        private readonly UnityBatchmodeProcessLaunchResult result;

        public StubUnityBatchmodeProcessLauncher (UnityBatchmodeProcessLaunchResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public ValueTask<UnityBatchmodeProcessLaunchResult> LaunchAsync (
            ResolvedUnityProjectContext unityProject,
            IpcBatchmodeBootstrapArguments bootstrapArguments,
            string unityLogPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubUnityUcliPluginLocator : IUnityUcliPluginLocator
    {
        public int CallCount { get; private set; }

        public Func<CancellationToken, ValueTask<UnityUcliPluginLocateResult>>? Handler { get; set; }

        public bool ObservedCancellation { get; private set; }

        public UnityUcliPluginLocateResult Result { get; set; }
            = UnityUcliPluginLocateResult.Found(
                "/tmp/ucli-plugin.json",
                UnityUcliPluginLocator.ExpectedProtocolVersion);

        public ValueTask<UnityUcliPluginLocateResult> LocateAsync (
            string unityProjectRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (Handler == null)
            {
                return ValueTask.FromResult(Result);
            }

            return LocateCoreAsync(cancellationToken);
        }

        private async ValueTask<UnityUcliPluginLocateResult> LocateCoreAsync (CancellationToken cancellationToken)
        {
            try
            {
                return await Handler!(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                ObservedCancellation = true;
                throw;
            }
        }
    }

    private sealed class StubUnityBatchmodeProcessHandle : IUnityBatchmodeProcessHandle
    {
        public int ProcessId => 1234;

        public bool HasExited { get; private set; }

        public int? ExitCode => HasExited ? 0 : null;

        public Task WaitForExitAsync (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HasExited = true;
            return Task.CompletedTask;
        }

        public Task<ProcessTerminationResult> TerminateAsync (
            ProcessTerminationPolicy? terminationPolicy = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HasExited = true;
            return Task.FromResult(ProcessTerminationResult.GracefulExited);
        }

        public ValueTask DisposeAsync ()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubIpcEndpointResolver : IIpcEndpointResolver
    {
        private readonly IpcEndpoint endpoint;

        public StubIpcEndpointResolver (IpcEndpoint endpoint)
        {
            this.endpoint = endpoint;
        }

        public IpcEndpoint Resolve (
            string storageRoot,
            string projectFingerprint)
        {
            return endpoint;
        }
    }

    private sealed class StubUnityProjectLockFileProbe : IUnityProjectLockFileProbe
    {
        public UnityProjectLockFileProbeResult Probe (string unityProjectRoot)
        {
            return UnityProjectLockFileProbeResult.Unlocked("/tmp/unity-project/Temp/UnityLockfile");
        }
    }

}

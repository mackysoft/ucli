using System.Text.Json;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcRequestExecutorTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenModeDecisionReturnsContractError_ReturnsContractFailureWithoutCallingClients ()
    {
        var daemonClient = new StubUnityIpcClient();
        var oneshotClient = new StubUnityOneshotIpcClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider();
        var executor = new UnityIpcRequestExecutor(
            new StubModeDecisionService(
                UnityExecutionModeDecisionResult.ContractFailure(
                    new UnityExecutionModeDecisionContractError(
                        UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                        "Daemon is not running for mode=daemon."))),
            daemonClient,
            oneshotClient,
            sessionTokenProvider);

        var result = await executor.Execute(
            UcliCommandIds.Ops,
            "daemon",
            null,
            UcliConfig.CreateDefault(),
            CreateContext(),
            IpcMethodNames.OpsRead,
            EmptyPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        Assert.Equal(0, daemonClient.CallCount);
        Assert.Equal(0, oneshotClient.CallCount);
        Assert.Equal(0, sessionTokenProvider.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTargetIsDaemon_UsesDaemonClient ()
    {
        var response = CreateResponse("req-1");
        var daemonClient = new StubUnityIpcClient
        {
            Response = response,
        };
        var oneshotClient = new StubUnityOneshotIpcClient();
        var sessionTokenProvider = new StubDaemonSessionTokenProvider
        {
            Result = DaemonSessionTokenResolutionResult.Success("daemon-token"),
        };
        var executor = new UnityIpcRequestExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout))),
            daemonClient,
            oneshotClient,
            sessionTokenProvider);

        var result = await executor.Execute(
            UcliCommandIds.Ops,
            null,
            null,
            UcliConfig.CreateDefault(),
            CreateContext(),
            IpcMethodNames.OpsRead,
            EmptyPayload());

        Assert.True(result.IsSuccess);
        Assert.Same(response, result.Response);
        Assert.Equal(1, daemonClient.CallCount);
        Assert.Equal(0, oneshotClient.CallCount);
        Assert.Equal(1, sessionTokenProvider.CallCount);
        Assert.Equal("daemon-token", daemonClient.LastRequest!.SessionToken);
        Assert.Equal(IpcMethodNames.OpsRead, daemonClient.LastRequest.Method);
        Assert.Equal(DefaultTimeout, daemonClient.LastTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTargetIsOneshot_UsesOneshotClient ()
    {
        var response = CreateResponse("req-2");
        var daemonClient = new StubUnityIpcClient();
        var oneshotClient = new StubUnityOneshotIpcClient
        {
            Result = UnityIpcRequestExecutionResult.Success(response),
        };
        var sessionTokenProvider = new StubDaemonSessionTokenProvider();
        var executor = new UnityIpcRequestExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    false,
                    UnityExecutionTarget.Oneshot,
                    DefaultTimeout))),
            daemonClient,
            oneshotClient,
            sessionTokenProvider);

        var result = await executor.Execute(
            UcliCommandIds.Ops,
            null,
            null,
            UcliConfig.CreateDefault(),
            CreateContext(),
            IpcMethodNames.OpsRead,
            EmptyPayload());

        Assert.True(result.IsSuccess);
        Assert.Same(response, result.Response);
        Assert.Equal(0, daemonClient.CallCount);
        Assert.Equal(1, oneshotClient.CallCount);
        Assert.Equal(0, sessionTokenProvider.CallCount);
        Assert.Equal("oneshot", oneshotClient.LastRequest!.SessionToken);
        Assert.Equal(IpcMethodNames.OpsRead, oneshotClient.LastRequest.Method);
        Assert.Equal(DefaultTimeout, oneshotClient.LastTimeout);
    }

    private static ResolvedUnityProjectContext CreateContext ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
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

    private sealed class StubModeDecisionService : IUnityExecutionModeDecisionService
    {
        private readonly UnityExecutionModeDecisionResult result;

        public StubModeDecisionService (UnityExecutionModeDecisionResult result)
        {
            this.result = result;
        }

        public ValueTask<UnityExecutionModeDecisionResult> Decide (
            UcliCommand command,
            string? mode,
            string? timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubUnityIpcClient : IUnityIpcClient
    {
        public int CallCount { get; private set; }

        public IpcRequest? LastRequest { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public IpcResponse Response { get; set; } = CreateResponse("default-daemon");

        public ValueTask<IpcResponse> SendAsync (
            string storageRoot,
            string projectFingerprint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            LastTimeout = timeout;
            return ValueTask.FromResult(Response);
        }
    }

    private sealed class StubUnityOneshotIpcClient : IUnityOneshotIpcClient
    {
        public int CallCount { get; private set; }

        public IpcRequest? LastRequest { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public UnityIpcRequestExecutionResult Result { get; set; }
            = UnityIpcRequestExecutionResult.Success(CreateResponse("default-oneshot"));

        public ValueTask<UnityIpcRequestExecutionResult> SendAsync (
            string unityProjectRoot,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            LastTimeout = timeout;
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubDaemonSessionTokenProvider : IDaemonSessionTokenProvider
    {
        public int CallCount { get; private set; }

        public DaemonSessionTokenResolutionResult Result { get; set; }
            = DaemonSessionTokenResolutionResult.SessionNotAvailable();

        public ValueTask<DaemonSessionTokenResolutionResult> Resolve (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(Result);
        }
    }
}
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;
using MackySoft.Ucli.UnityProject.Resolution;

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
        var executor = new UnityIpcRequestExecutor(
            new StubModeDecisionService(
                UnityExecutionModeDecisionResult.ContractFailure(
                    new UnityExecutionModeDecisionContractError(
                        UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                        "Daemon is not running for mode=daemon."))),
            new StubUnityUcliPluginLocator(),
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher));

        var result = await executor.Execute(
            UcliCommandIds.Ops,
            "daemon",
            null,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            IpcMethodNames.OpsRead,
            EmptyPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
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
        var executor = new UnityIpcRequestExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout))),
            pluginLocator,
            CreateClients(daemonTransportClient, oneshotTransportClient, sessionTokenProvider, launcher));

        var result = await executor.Execute(
            UcliCommandIds.Ops,
            null,
            null,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            IpcMethodNames.OpsRead,
            EmptyPayload());

        Assert.True(result.IsSuccess);
        Assert.Same(response, result.Response);
        Assert.Equal(1, daemonTransportClient.CallCount);
        Assert.Equal(0, oneshotTransportClient.CallCount);
        Assert.Equal(1, sessionTokenProvider.CallCount);
        Assert.Equal("daemon-token", daemonTransportClient.Requests[0].SessionToken);
        Assert.Equal(0, pluginLocator.CallCount);
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
                IpcMethodNames.Ping => CreateResponse(request.RequestId),
                IpcMethodNames.OpsRead => response,
                _ => throw new Xunit.Sdk.XunitException($"Unexpected method: {request.Method}"),
            };
        });
        var launcher = new StubUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var executor = new UnityIpcRequestExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    false,
                    UnityExecutionTarget.Oneshot,
                    DefaultTimeout))),
            new StubUnityUcliPluginLocator(),
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher));

        var result = await executor.Execute(
            UcliCommandIds.Ops,
            null,
            null,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            IpcMethodNames.OpsRead,
            EmptyPayload());

        Assert.True(result.IsSuccess);
        Assert.Same(response, result.Response);
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
        var executor = new UnityIpcRequestExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    false,
                    UnityExecutionTarget.Oneshot,
                    DefaultTimeout))),
            pluginLocator,
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher));

        var result = await executor.Execute(
            UcliCommandIds.Ops,
            "oneshot",
            null,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            IpcMethodNames.OpsRead,
            EmptyPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
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
        var executor = new UnityIpcRequestExecutor(
            new StubModeDecisionService(
                UnityExecutionModeDecisionResult.ContractFailure(
                    new UnityExecutionModeDecisionContractError(
                        UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                        "Daemon is not running for mode=daemon."))),
            pluginLocator,
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher));

        var result = await executor.Execute(
            UcliCommandIds.Ops,
            "daemon",
            null,
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            IpcMethodNames.OpsRead,
            EmptyPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
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
        var executor = new UnityIpcRequestExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    false,
                    UnityExecutionTarget.Oneshot,
                    TimeSpan.FromMilliseconds(120)))),
            pluginLocator,
            CreateClients(daemonTransportClient, oneshotTransportClient, new StubDaemonSessionTokenProvider(), launcher));

        var result = await executor.Execute(
            UcliCommandIds.Ops,
            "oneshot",
            "120",
            UcliConfig.CreateDefault(),
            CreateContext(scope),
            IpcMethodNames.OpsRead,
            EmptyPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(CliErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.True(pluginLocator.ObservedCancellation);
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
                new StubProjectLifecycleLockProvider()),
        ];
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

        public ValueTask<DaemonSessionTokenResolutionResult> Resolve (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(Result);
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

        public ValueTask<UnityBatchmodeProcessLaunchResult> Launch (
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

        public ValueTask<UnityUcliPluginLocateResult> Locate (
            string unityProjectRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
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

        public Task WaitForExit (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HasExited = true;
            return Task.CompletedTask;
        }

        public Task Terminate (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HasExited = true;
            return Task.CompletedTask;
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

    private sealed class StubProjectLifecycleLockProvider : IProjectLifecycleLockProvider
    {
        public ValueTask<IAsyncDisposable> Acquire (
            string storageRoot,
            string projectFingerprint,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IAsyncDisposable>(new NoOpAsyncDisposable());
        }
    }

    private sealed class NoOpAsyncDisposable : IAsyncDisposable
    {
        public ValueTask DisposeAsync ()
        {
            return ValueTask.CompletedTask;
        }
    }
}
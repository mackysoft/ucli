using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Status;
using MackySoft.Ucli.UnityProject;
using MackySoft.Ucli.UnityProject.Resolution;

namespace MackySoft.Ucli.Tests.Status;

public sealed class StatusServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonIsRunning_ReturnsPingInfoAndResolvedUnityVersion ()
    {
        var contextResolver = new StubInitStatusContextResolver(InitStatusContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonManagementService = new StubDaemonManagementService(DaemonStatusResult.Running(CreateSession("session-token")));
        var daemonPingInfoClient = new StubDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "batchmode",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonManagementService,
            daemonPingInfoClient);

        var result = await service.Execute(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<StatusExecutionOutput>(result.Output);
        Assert.Equal("running", output.DaemonStatus);
        Assert.Equal("6000.1.4f1", output.UnityVersion);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal("ready", output.CompileState);
        Assert.Equal("batchmode", output.Runtime);
        Assert.Equal(UcliConfig.DefaultIpcTimeoutMilliseconds, output.TimeoutMilliseconds);
        Assert.Equal("session-token", daemonPingInfoClient.LastSessionToken);
        Assert.Equal(1, daemonPingInfoClient.CallCount);
        Assert.Equal(1, daemonManagementService.GetStatusCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonIsNotRunning_ReturnsStatusWithNullPingFields ()
    {
        var contextResolver = new StubInitStatusContextResolver(InitStatusContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonManagementService = new StubDaemonManagementService(DaemonStatusResult.NotRunning());
        var daemonPingInfoClient = new StubDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "batchmode",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonManagementService,
            daemonPingInfoClient);

        var result = await service.Execute(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<StatusExecutionOutput>(result.Output);
        Assert.Equal("notRunning", output.DaemonStatus);
        Assert.Equal("6000.1.4f1", output.UnityVersion);
        Assert.Null(output.ServerVersion);
        Assert.Null(output.CompileState);
        Assert.Null(output.Runtime);
        Assert.Equal(0, daemonPingInfoClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonIsStale_ReturnsStatusWithNullPingFields ()
    {
        var contextResolver = new StubInitStatusContextResolver(InitStatusContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonManagementService = new StubDaemonManagementService(DaemonStatusResult.Stale(CreateSession("stale-session-token")));
        var daemonPingInfoClient = new StubDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "batchmode",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonManagementService,
            daemonPingInfoClient);

        var result = await service.Execute(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<StatusExecutionOutput>(result.Output);
        Assert.Equal("stale", output.DaemonStatus);
        Assert.Equal("6000.1.4f1", output.UnityVersion);
        Assert.Null(output.ServerVersion);
        Assert.Null(output.CompileState);
        Assert.Null(output.Runtime);
        Assert.Equal(0, daemonPingInfoClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTimeoutOptionIsInvalid_ReturnsInvalidArgumentWithoutDaemonCall ()
    {
        var contextResolver = new StubInitStatusContextResolver(InitStatusContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonManagementService = new StubDaemonManagementService(DaemonStatusResult.NotRunning());
        var daemonPingInfoClient = new StubDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "batchmode",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonManagementService,
            daemonPingInfoClient);

        var result = await service.Execute(projectPath: null, timeout: "abc", cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("timeout", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, daemonManagementService.GetStatusCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenContextResolutionFails_ReturnsResolutionError ()
    {
        var contextResolver = new StubInitStatusContextResolver(InitStatusContextResolutionResult.Failure(
            ExecutionError.InvalidArgument("Unity project path is invalid.")));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonManagementService = new StubDaemonManagementService(DaemonStatusResult.NotRunning());
        var daemonPingInfoClient = new StubDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "batchmode",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonManagementService,
            daemonPingInfoClient);

        var result = await service.Execute(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal("Unity project path is invalid.", error.Message);
        Assert.Equal(0, daemonManagementService.GetStatusCallCount);
        Assert.Equal(0, unityVersionResolver.ResolveCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonStatusFails_ReturnsDaemonStatusError ()
    {
        var contextResolver = new StubInitStatusContextResolver(InitStatusContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonManagementService = new StubDaemonManagementService(DaemonStatusResult.Failure(
            ExecutionError.InternalError("Failed to read daemon session.")));
        var daemonPingInfoClient = new StubDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "batchmode",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonManagementService,
            daemonPingInfoClient);

        var result = await service.Execute(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Failed to read daemon session.", error.Message);
        Assert.Equal(0, daemonPingInfoClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPingInfoFails_ReturnsInternalError ()
    {
        var contextResolver = new StubInitStatusContextResolver(InitStatusContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonManagementService = new StubDaemonManagementService(DaemonStatusResult.Running(CreateSession("session-token")));
        var daemonPingInfoClient = new StubDaemonPingInfoClient(
            nextException: new DaemonPingResponseException("failed"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonManagementService,
            daemonPingInfoClient);

        var result = await service.Execute(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to read daemon ping information", error.Message, StringComparison.Ordinal);
    }

    private static StatusService CreateService (
        IInitStatusContextResolver contextResolver,
        IUnityVersionResolver unityVersionResolver,
        IDaemonManagementService daemonManagementService,
        IDaemonPingInfoClient daemonPingInfoClient)
    {
        return new StatusService(
            new StatusExecutionContextResolver(contextResolver, unityVersionResolver),
            new StatusDaemonObservationService(daemonManagementService, daemonPingInfoClient));
    }

    private static InitStatusContext CreateContext ()
    {
        var unityProjectRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"));
        return new InitStatusContext(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: unityProjectRoot,
                RepositoryRoot: unityProjectRoot,
                ProjectFingerprint: "project-fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            Config: UcliConfig.CreateDefault(),
            ConfigSource: ConfigSource.Default);
    }

    private static DaemonSession CreateSession (string sessionToken)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: sessionToken,
            ProjectFingerprint: "project-fingerprint",
            IssuedAtUtc: DateTimeOffset.UtcNow,
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindCli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-status",
            ProcessId: 1234);
    }

    private sealed class StubInitStatusContextResolver : IInitStatusContextResolver
    {
        private readonly InitStatusContextResolutionResult resolutionResult;

        public StubInitStatusContextResolver (InitStatusContextResolutionResult resolutionResult)
        {
            this.resolutionResult = resolutionResult;
        }

        public ValueTask<InitStatusContextResolutionResult> Resolve (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(resolutionResult);
        }
    }

    private sealed class StubUnityVersionResolver : IUnityVersionResolver
    {
        private readonly UnityVersionResolutionResult resolutionResult;

        public StubUnityVersionResolver (UnityVersionResolutionResult resolutionResult)
        {
            this.resolutionResult = resolutionResult;
        }

        public int ResolveCallCount { get; private set; }

        public UnityVersionResolutionResult Resolve (
            string projectPath,
            string? preferredUnityVersion)
        {
            ResolveCallCount++;
            return resolutionResult;
        }
    }

    private sealed class StubDaemonManagementService : IDaemonManagementService
    {
        private readonly DaemonStatusResult daemonStatusResult;

        public StubDaemonManagementService (DaemonStatusResult daemonStatusResult)
        {
            this.daemonStatusResult = daemonStatusResult;
        }

        public int GetStatusCallCount { get; private set; }

        public ValueTask<DaemonStartResult> Start (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonStopResult> Stop (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonStatusResult> GetStatus (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            GetStatusCallCount++;
            return ValueTask.FromResult(daemonStatusResult);
        }

        public ValueTask<DaemonLogReadResult> ReadLogs (
            ResolvedUnityProjectContext unityProject,
            int maxBytes = DaemonLogReader.DefaultMaxBytes,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubDaemonPingInfoClient : IDaemonPingInfoClient
    {
        private readonly IpcPingResponse? nextResult;

        private readonly Exception? nextException;

        public StubDaemonPingInfoClient (
            IpcPingResponse? nextResult = null,
            Exception? nextException = null)
        {
            this.nextResult = nextResult;
            this.nextException = nextException;
        }

        public int CallCount { get; private set; }

        public string? LastSessionToken { get; private set; }

        public ValueTask<IpcPingResponse> PingAndRead (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastSessionToken = sessionToken;

            if (nextException is not null)
            {
                return ValueTask.FromException<IpcPingResponse>(nextException);
            }

            return ValueTask.FromResult(nextResult!);
        }
    }
}
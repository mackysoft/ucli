using System.Net.Sockets;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Features.Status.Common.Contracts;
using MackySoft.Ucli.Features.Status.UseCases.Status;
using MackySoft.Ucli.Features.Status.UseCases.Status.Observation;
using MackySoft.Ucli.Features.Status.UseCases.Status.Preflight;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project;
using MackySoft.Ucli.UnityIntegration.Resolution;

namespace MackySoft.Ucli.Tests.Status;

public sealed class StatusServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonIsRunning_ReturnsPingInfoAndResolvedUnityVersion ()
    {
        var contextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new StubDaemonStatusOperation(DaemonStatusResult.Running(CreateSession("session-token")));
        var daemonPingInfoClient = new StubDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "batchmode",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready",
            LifecycleState: "busy",
            BlockingReason: "busy",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: false));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.Execute(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<StatusExecutionOutput>(result.Output);
        Assert.Equal("running", output.DaemonStatus);
        Assert.Equal("6000.1.4f1", output.UnityVersion);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal("busy", output.LifecycleState);
        Assert.Equal("busy", output.BlockingReason);
        Assert.Equal("ready", output.CompileState);
        Assert.Equal("12", output.CompileGeneration);
        Assert.Equal("7", output.DomainReloadGeneration);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Equal("batchmode", output.Runtime);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultStatusMilliseconds, output.TimeoutMilliseconds);
        Assert.Equal("session-token", daemonPingInfoClient.LastSessionToken);
        Assert.Equal(1, daemonPingInfoClient.CallCount);
        Assert.Equal(1, daemonStatusOperation.GetStatusCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonIsNotRunning_ReturnsStatusWithNullPingFields ()
    {
        var contextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new StubDaemonStatusOperation(DaemonStatusResult.NotRunning());
        var daemonPingInfoClient = new StubDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "batchmode",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.Execute(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<StatusExecutionOutput>(result.Output);
        Assert.Equal("notRunning", output.DaemonStatus);
        Assert.Equal("6000.1.4f1", output.UnityVersion);
        Assert.Null(output.ServerVersion);
        Assert.Null(output.LifecycleState);
        Assert.Null(output.BlockingReason);
        Assert.Null(output.CompileState);
        Assert.Null(output.CompileGeneration);
        Assert.Null(output.DomainReloadGeneration);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Null(output.Runtime);
        Assert.Equal(0, daemonPingInfoClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonIsStale_ReturnsStatusWithNullPingFields ()
    {
        var contextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new StubDaemonStatusOperation(DaemonStatusResult.Stale(CreateSession("stale-session-token")));
        var daemonPingInfoClient = new StubDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "batchmode",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.Execute(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<StatusExecutionOutput>(result.Output);
        Assert.Equal("stale", output.DaemonStatus);
        Assert.Equal("6000.1.4f1", output.UnityVersion);
        Assert.Null(output.ServerVersion);
        Assert.Null(output.LifecycleState);
        Assert.Null(output.BlockingReason);
        Assert.Null(output.CompileState);
        Assert.Null(output.CompileGeneration);
        Assert.Null(output.DomainReloadGeneration);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Null(output.Runtime);
        Assert.Equal(0, daemonPingInfoClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTimeoutMillisecondsIsInvalid_ReturnsInvalidArgumentWithoutDaemonCall ()
    {
        var contextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new StubDaemonStatusOperation(DaemonStatusResult.NotRunning());
        var daemonPingInfoClient = new StubDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "batchmode",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.Execute(new StatusCommandInput(null, 0), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("timeout", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, daemonStatusOperation.GetStatusCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenContextResolutionFails_ReturnsResolutionError ()
    {
        var contextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Failure(
            ExecutionError.InvalidArgument("Unity project path is invalid.")));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new StubDaemonStatusOperation(DaemonStatusResult.NotRunning());
        var daemonPingInfoClient = new StubDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "batchmode",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.Execute(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal("Unity project path is invalid.", error.Message);
        Assert.Equal(0, daemonStatusOperation.GetStatusCallCount);
        Assert.Equal(0, unityVersionResolver.ResolveCallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonStatusFails_ReturnsDaemonStatusError ()
    {
        var contextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new StubDaemonStatusOperation(DaemonStatusResult.Failure(
            ExecutionError.InternalError("Failed to read daemon session.")));
        var daemonPingInfoClient = new StubDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            Runtime: "batchmode",
            UnityVersion: "2022.3.5f1",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.Execute(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Failed to read daemon session.", error.Message);
        Assert.Equal(0, daemonPingInfoClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPingInfoTimesOut_ReturnsTimeoutError ()
    {
        var contextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new StubDaemonStatusOperation(DaemonStatusResult.Running(CreateSession("session-token")));
        var daemonPingInfoClient = new StubDaemonPingInfoClient(
            nextException: new TimeoutException("ping timeout"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.Execute(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("Timed out while reading daemon ping information.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPingInfoBecomesUnreachable_ReturnsStaleStatus ()
    {
        var contextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new StubDaemonStatusOperation(DaemonStatusResult.Running(CreateSession("session-token")));
        var daemonPingInfoClient = new StubDaemonPingInfoClient(
            nextException: new SocketException((int)SocketError.ConnectionRefused));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.Execute(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<StatusExecutionOutput>(result.Output);
        Assert.Equal("stale", output.DaemonStatus);
        Assert.Null(output.ServerVersion);
        Assert.Null(output.CompileState);
        Assert.Null(output.Runtime);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPingInfoFails_ReturnsInternalError ()
    {
        var contextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new StubDaemonStatusOperation(DaemonStatusResult.Running(CreateSession("session-token")));
        var daemonPingInfoClient = new StubDaemonPingInfoClient(
            nextException: new DaemonPingResponseException("failed"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.Execute(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to read daemon ping information", error.Message, StringComparison.Ordinal);
    }

    private static StatusService CreateService (
        IProjectContextResolver contextResolver,
        IUnityVersionResolver unityVersionResolver,
        IDaemonStatusOperation daemonStatusOperation,
        IDaemonPingInfoClient daemonPingInfoClient)
    {
        return new StatusService(
            new StatusExecutionContextResolver(contextResolver, unityVersionResolver),
            new StatusDaemonObservationService(
                daemonStatusOperation,
                daemonPingInfoClient,
                new DaemonReachabilityClassifier()));
    }

    private static ProjectContext CreateContext ()
    {
        var unityProjectRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"));
        return new ProjectContext(
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
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-status",
            ProcessId: 1234,

            OwnerProcessId: 9876);
    }

    private sealed class StubProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContextResolutionResult resolutionResult;

        public StubProjectContextResolver (ProjectContextResolutionResult resolutionResult)
        {
            this.resolutionResult = resolutionResult;
        }

        public ValueTask<ProjectContextResolutionResult> Resolve (
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

    private sealed class StubDaemonStatusOperation : IDaemonStatusOperation
    {
        private readonly DaemonStatusResult daemonStatusResult;

        public StubDaemonStatusOperation (DaemonStatusResult daemonStatusResult)
        {
            this.daemonStatusResult = daemonStatusResult;
        }

        public int GetStatusCallCount { get; private set; }

        public ValueTask<DaemonStatusResult> GetStatus (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            GetStatusCallCount++;
            return ValueTask.FromResult(daemonStatusResult);
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
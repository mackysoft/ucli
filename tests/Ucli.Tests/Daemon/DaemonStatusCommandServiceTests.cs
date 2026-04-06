using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonStatusCommandServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonIsStale_ReturnsStaleOutputWithMappedSession ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 5678);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Stale(DaemonCommandServiceTestContext.CreateSession()),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper
        {
            Output = DaemonCommandServiceTestContext.CreateSessionOutput(),
        };
        var diagnosisMapper = new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper();
        var service = CreateService(resolver, daemonStatusOperation, mapper, diagnosisMapper);

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal("stale", output.DaemonStatus);
        Assert.Equal(5678, output.TimeoutMilliseconds);
        Assert.Equal(mapper.Output, output.Session);
        Assert.Null(output.Diagnosis);
        Assert.Null(output.ServerVersion);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(1, daemonStatusOperation.GetStatusCallCount);
        Assert.Equal(1, mapper.CallCount);
        Assert.Equal(0, diagnosisMapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonStatusOperationFails_ReturnsFailure ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1000);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Failure(ExecutionError.InternalError("status failed")),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            mapper,
            new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("status failed", error.Message);
        Assert.Equal(1, daemonStatusOperation.GetStatusCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonStatusOperationTimesOut_ReturnsTimeoutFailure ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1000);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Failure(ExecutionError.Timeout("probe timeout")),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            mapper,
            new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal("probe timeout", error.Message);
        Assert.Equal(1, daemonStatusOperation.GetStatusCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonIsNotRunning_ReturnsOutputWithoutSession ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2222);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.NotRunning(),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            mapper,
            new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal("notRunning", output.DaemonStatus);
        Assert.Equal(2222, output.TimeoutMilliseconds);
        Assert.Null(output.Session);
        Assert.Null(output.Diagnosis);
        Assert.Null(output.ServerVersion);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Equal(1, daemonStatusOperation.GetStatusCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonStatusOperationFailsWithoutError_ReturnsFallbackInternalError ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1300);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = new DaemonStatusResult(DaemonStatusKind.Failed, null, null, null),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            mapper,
            new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Daemon status operation failed without structured error details.", error.Message);
        Assert.Equal(1, daemonStatusOperation.GetStatusCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenStatusLiteralIsUnsupported_ReturnsInternalError ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1400);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = new DaemonStatusResult(
                Status: (DaemonStatusKind)int.MaxValue,
                Session: DaemonCommandServiceTestContext.CreateSession(),
                Diagnosis: null,
                Error: null),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            mapper,
            new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal($"Daemon status returned unsupported status: {(DaemonStatusKind)int.MaxValue}.", error.Message);
        Assert.Equal(1, daemonStatusOperation.GetStatusCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenArgumentsAreSpecified_PropagatesToResolverAndOperation ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2300);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.NotRunning(),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            mapper,
            new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper(),
            timeProvider: timeProvider);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await service.GetStatus(
            projectPath: "/tmp/sandbox-unity",
            timeout: "9999",
            cancellationToken: cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(UcliCommandIds.DaemonStatus, resolver.LastTimeoutCommand);
        Assert.Equal("/tmp/sandbox-unity", resolver.LastProjectPath);
        Assert.Equal("9999", resolver.LastTimeoutOption);
        Assert.Equal(cancellationToken, resolver.LastCancellationToken);
        Assert.Equal(context.Context.UnityProject, daemonStatusOperation.LastUnityProject);
        Assert.Equal(context.Timeout, daemonStatusOperation.LastTimeout);
        Assert.Equal(cancellationToken, daemonStatusOperation.LastCancellationToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDiagnosisExists_MapsDiagnosisToOutput ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2400);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var diagnosis = DaemonCommandServiceTestContext.CreateDiagnosis();
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.NotRunning(diagnosis),
        };
        var mapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var diagnosisMapper = new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper();
        var service = CreateService(resolver, daemonStatusOperation, mapper, diagnosisMapper);

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.NotNull(output.Diagnosis);
        Assert.Equal(diagnosisMapper.Output, output.Diagnosis);
        Assert.Equal(1, diagnosisMapper.CallCount);
        Assert.Equal(diagnosis, diagnosisMapper.LastDiagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonIsRunning_MapsPingTelemetryToOutput ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2450);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonCommandServiceTestContext.CreateSession();
        var persistedDiagnosis = DaemonCommandServiceTestContext.CreateDiagnosis();
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(session, persistedDiagnosis),
        };
        var pingInfoClient = new DaemonCommandServiceTestContext.StubDaemonPingInfoClient
        {
            Response = new IpcPingResponse(
                ServerVersion: "9.9.9",
                Runtime: "batchmode",
                UnityVersion: "6000.1.4f1",
                CompileState: IpcCompileStateCodec.Compiling,
                LifecycleState: IpcEditorLifecycleStateCodec.DomainReloading,
                BlockingReason: IpcEditorBlockingReasonCodec.DomainReload,
                CompileGeneration: "7",
                DomainReloadGeneration: "11",
                CanAcceptExecutionRequests: false),
        };
        var sessionMapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var diagnosisMapper = new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonCommandServiceTestContext.StubDaemonReachabilityClassifier(static _ => false),
            new DaemonCommandServiceTestContext.StubDaemonSessionDiagnosisResolver(),
            sessionMapper,
            diagnosisMapper,
            timeProvider);

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal("running", output.DaemonStatus);
        Assert.Equal("9.9.9", output.ServerVersion);
        Assert.Equal("batchmode", output.Runtime);
        Assert.Equal(IpcEditorLifecycleStateCodec.DomainReloading, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReasonCodec.DomainReload, output.BlockingReason);
        Assert.Equal(IpcCompileStateCodec.Compiling, output.CompileState);
        Assert.Equal("7", output.CompileGeneration);
        Assert.Equal("11", output.DomainReloadGeneration);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Equal(sessionMapper.Output, output.Session);
        Assert.Null(output.Diagnosis);
        Assert.Equal(1, pingInfoClient.CallCount);
        Assert.Equal(context.Context.UnityProject, pingInfoClient.LastUnityProject);
        Assert.Equal(context.Timeout, pingInfoClient.LastTimeout);
        Assert.Equal(session.SessionToken, pingInfoClient.LastSessionToken);
        Assert.Equal(1, sessionMapper.CallCount);
        Assert.Equal(session, sessionMapper.LastSession);
        Assert.Equal(0, diagnosisMapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenProbeConsumesBudget_PropagatesRemainingTimeoutToPingInfoRead ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 700);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(DaemonCommandServiceTestContext.CreateSession()),
            OnGetStatus = () => timeProvider.Advance(TimeSpan.FromMilliseconds(200)),
        };
        var pingInfoClient = new DaemonCommandServiceTestContext.StubDaemonPingInfoClient();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonCommandServiceTestContext.StubDaemonReachabilityClassifier(static _ => false),
            new DaemonCommandServiceTestContext.StubDaemonSessionDiagnosisResolver(),
            new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper(),
            new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper(),
            timeProvider);

        var result = await service.GetStatus(projectPath: null, timeout: "700", cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, pingInfoClient.CallCount);
        Assert.Equal(TimeSpan.FromMilliseconds(500), pingInfoClient.LastTimeout);
        Assert.True(pingInfoClient.LastTimeout < context.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenProbeConsumesEntireBudget_ReturnsTimeoutBeforePingInfoRead ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 300);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(DaemonCommandServiceTestContext.CreateSession()),
            OnGetStatus = () => timeProvider.Advance(TimeSpan.FromMilliseconds(300)),
        };
        var pingInfoClient = new DaemonCommandServiceTestContext.StubDaemonPingInfoClient();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonCommandServiceTestContext.StubDaemonReachabilityClassifier(static _ => false),
            new DaemonCommandServiceTestContext.StubDaemonSessionDiagnosisResolver(),
            new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper(),
            new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper(),
            timeProvider);

        var result = await service.GetStatus(projectPath: null, timeout: "300", cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal("Timed out before daemon ping information read could begin.", error.Message);
        Assert.Equal(0, pingInfoClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenRunningSessionIsMissing_ReturnsInternalError ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2475);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = new DaemonStatusResult(
                Status: DaemonStatusKind.Running,
                Session: null,
                Diagnosis: null,
                Error: null),
        };
        var sessionMapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            sessionMapper,
            new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Daemon status is running but daemon session is missing.", error.Message);
        Assert.Equal(0, sessionMapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenRunningPingInfoReadTimesOut_ReturnsTimeoutFailure ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2480);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(DaemonCommandServiceTestContext.CreateSession()),
        };
        var pingInfoClient = new DaemonCommandServiceTestContext.StubDaemonPingInfoClient
        {
            Exception = new TimeoutException("ping timeout"),
        };
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonCommandServiceTestContext.StubDaemonReachabilityClassifier(static _ => false),
            new DaemonCommandServiceTestContext.StubDaemonSessionDiagnosisResolver(),
            new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper(),
            new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal("Timed out while reading daemon ping information. ping timeout", error.Message);
        Assert.Equal(1, pingInfoClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenRunningPingInfoReadFailsUnexpectedly_ReturnsInternalError ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2490);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(DaemonCommandServiceTestContext.CreateSession()),
        };
        var pingInfoClient = new DaemonCommandServiceTestContext.StubDaemonPingInfoClient
        {
            Exception = new InvalidOperationException("broken pipe"),
        };
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonCommandServiceTestContext.StubDaemonReachabilityClassifier(static _ => false),
            new DaemonCommandServiceTestContext.StubDaemonSessionDiagnosisResolver(),
            new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper(),
            new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Failed to read daemon ping information. broken pipe", error.Message);
        Assert.Equal(1, pingInfoClient.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenPingInfoReadFallsBackToStale_ResolvesDiagnosisForSession ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2500);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonCommandServiceTestContext.CreateSession();
        var persistedDiagnosis = DaemonCommandServiceTestContext.CreateDiagnosis();
        var diagnosis = DaemonCommandServiceTestContext.CreateDiagnosis();
        var daemonStatusOperation = new DaemonCommandServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(session, persistedDiagnosis),
        };
        var pingInfoClient = new DaemonCommandServiceTestContext.StubDaemonPingInfoClient
        {
            Exception = new InvalidOperationException("daemon exited"),
        };
        var reachabilityClassifier = new DaemonCommandServiceTestContext.StubDaemonReachabilityClassifier(static _ => true);
        var diagnosisResolver = new DaemonCommandServiceTestContext.StubDaemonSessionDiagnosisResolver
        {
            Diagnosis = diagnosis,
        };
        var sessionMapper = new DaemonCommandServiceTestContext.StubDaemonSessionOutputMapper();
        var diagnosisMapper = new DaemonCommandServiceTestContext.StubDaemonDiagnosisOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            reachabilityClassifier,
            diagnosisResolver,
            sessionMapper,
            diagnosisMapper);

        var result = await service.GetStatus(projectPath: null, timeout: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal("stale", output.DaemonStatus);
        Assert.Equal(sessionMapper.Output, output.Session);
        Assert.Equal(diagnosisMapper.Output, output.Diagnosis);
        Assert.Equal(1, pingInfoClient.CallCount);
        Assert.Equal(1, diagnosisResolver.CallCount);
        Assert.Equal(context.Context.UnityProject, diagnosisResolver.LastUnityProject);
        Assert.Equal(session, diagnosisResolver.LastSession);
        Assert.Equal(persistedDiagnosis, diagnosisResolver.LastPersistedDiagnosis);
        Assert.Equal(1, diagnosisMapper.CallCount);
        Assert.Equal(diagnosis, diagnosisMapper.LastDiagnosis);
    }

    private static DaemonStatusCommandService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        IDaemonStatusOperation daemonStatusOperation,
        IDaemonSessionOutputMapper sessionOutputMapper,
        IDaemonDiagnosisOutputMapper diagnosisOutputMapper,
        TimeProvider? timeProvider = null)
    {
        return CreateService(
            resolver,
            daemonStatusOperation,
            new DaemonCommandServiceTestContext.StubDaemonPingInfoClient(),
            new DaemonCommandServiceTestContext.StubDaemonReachabilityClassifier(static _ => false),
            new DaemonCommandServiceTestContext.StubDaemonSessionDiagnosisResolver(),
            sessionOutputMapper,
            diagnosisOutputMapper,
            timeProvider);
    }

    private static DaemonStatusCommandService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        IDaemonStatusOperation daemonStatusOperation,
        IDaemonPingInfoClient pingInfoClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonSessionDiagnosisResolver diagnosisResolver,
        IDaemonSessionOutputMapper sessionOutputMapper,
        IDaemonDiagnosisOutputMapper diagnosisOutputMapper,
        TimeProvider? timeProvider = null)
    {
        return new DaemonStatusCommandService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            reachabilityClassifier,
            diagnosisResolver,
            sessionOutputMapper,
            diagnosisOutputMapper,
            timeProvider);
    }
}
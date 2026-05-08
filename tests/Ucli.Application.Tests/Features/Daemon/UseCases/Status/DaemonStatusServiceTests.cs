using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStatusServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonIsStale_ReturnsStaleOutputWithMappedSession ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 5678);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Stale(DaemonServiceTestContext.CreateSession()),
        };
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper
        {
            Output = DaemonServiceTestContext.CreateSessionOutput(),
        };
        var diagnosisMapper = new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper();
        var service = CreateService(resolver, daemonStatusOperation, mapper, diagnosisMapper);

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Stale, output.DaemonStatus);
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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1000);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Failure(ExecutionError.InternalError("status failed")),
        };
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            mapper,
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1000);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Failure(ExecutionError.Timeout("probe timeout")),
        };
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            mapper,
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2222);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.NotRunning(),
        };
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            mapper,
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.NotRunning, output.DaemonStatus);
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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1300);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = new DaemonStatusResult(DaemonStatusKind.Failed, null, null, null),
        };
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            mapper,
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1400);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = new DaemonStatusResult(
                Status: (DaemonStatusKind)int.MaxValue,
                Session: DaemonServiceTestContext.CreateSession(),
                Diagnosis: null,
                Error: null),
        };
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            mapper,
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2300);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.NotRunning(),
        };
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            mapper,
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper(),
            timeProvider: timeProvider);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await service.GetStatus(
            projectPath: "/tmp/sandbox-unity",
            timeoutMilliseconds: 9999,
            cancellationToken: cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(UcliCommandIds.DaemonStatus, resolver.LastTimeoutCommand);
        Assert.Equal("/tmp/sandbox-unity", resolver.LastProjectPath);
        Assert.Equal(9999, resolver.LastTimeoutMilliseconds);
        Assert.Equal(cancellationToken, resolver.LastCancellationToken);
        Assert.Equal(context.Context.UnityProject, daemonStatusOperation.LastUnityProject);
        Assert.Equal(context.Timeout, daemonStatusOperation.LastTimeout);
        Assert.Equal(cancellationToken, daemonStatusOperation.LastCancellationToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDiagnosisExists_MapsDiagnosisToOutput ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2400);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var diagnosis = DaemonServiceTestContext.CreateDiagnosis();
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.NotRunning(diagnosis),
        };
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var diagnosisMapper = new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper();
        var service = CreateService(resolver, daemonStatusOperation, mapper, diagnosisMapper);

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2450);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonServiceTestContext.CreateSession();
        var persistedDiagnosis = DaemonServiceTestContext.CreateDiagnosis();
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(session, persistedDiagnosis),
        };
        var pingInfoClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Response = new IpcPingResponse(
                ServerVersion: "9.9.9",
                Runtime: "batchmode",
                UnityVersion: "6000.1.4f1",
                ProjectFingerprint: "project-fingerprint",
                CompileState: IpcCompileStateCodec.Compiling,
                LifecycleState: IpcEditorLifecycleStateCodec.DomainReloading,
                BlockingReason: IpcEditorBlockingReasonCodec.DomainReload,
                CompileGeneration: "7",
                DomainReloadGeneration: "11",
                CanAcceptExecutionRequests: false),
        };
        var sessionMapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var diagnosisMapper = new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonServiceTestContext.StubDaemonReachabilityClassifier(static _ => false),
            new DaemonServiceTestContext.StubDaemonSessionDiagnosisResolver(),
            sessionMapper,
            diagnosisMapper,
            timeProvider);

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal("9.9.9", output.ServerVersion);
        Assert.Equal("batchmode", output.EditorMode);
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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 700);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(DaemonServiceTestContext.CreateSession()),
            OnGetStatus = () => timeProvider.Advance(TimeSpan.FromMilliseconds(200)),
        };
        var pingInfoClient = new DaemonServiceTestContext.StubDaemonPingInfoClient();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonServiceTestContext.StubDaemonReachabilityClassifier(static _ => false),
            new DaemonServiceTestContext.StubDaemonSessionDiagnosisResolver(),
            new DaemonServiceTestContext.StubDaemonSessionOutputMapper(),
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper(),
            timeProvider);

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: 700, cancellationToken: CancellationToken.None);

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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 300);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(DaemonServiceTestContext.CreateSession()),
            OnGetStatus = () => timeProvider.Advance(TimeSpan.FromMilliseconds(300)),
        };
        var pingInfoClient = new DaemonServiceTestContext.StubDaemonPingInfoClient();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonServiceTestContext.StubDaemonReachabilityClassifier(static _ => false),
            new DaemonServiceTestContext.StubDaemonSessionDiagnosisResolver(),
            new DaemonServiceTestContext.StubDaemonSessionOutputMapper(),
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper(),
            timeProvider);

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: 300, cancellationToken: CancellationToken.None);

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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2475);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = new DaemonStatusResult(
                Status: DaemonStatusKind.Running,
                Session: null,
                Diagnosis: null,
                Error: null),
        };
        var sessionMapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            sessionMapper,
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2480);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(DaemonServiceTestContext.CreateSession()),
        };
        var pingInfoClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Exception = new TimeoutException("ping timeout"),
        };
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonServiceTestContext.StubDaemonReachabilityClassifier(static _ => false),
            new DaemonServiceTestContext.StubDaemonSessionDiagnosisResolver(),
            new DaemonServiceTestContext.StubDaemonSessionOutputMapper(),
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2490);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(DaemonServiceTestContext.CreateSession()),
        };
        var pingInfoClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Exception = new InvalidOperationException("broken pipe"),
        };
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonServiceTestContext.StubDaemonReachabilityClassifier(static _ => false),
            new DaemonServiceTestContext.StubDaemonSessionDiagnosisResolver(),
            new DaemonServiceTestContext.StubDaemonSessionOutputMapper(),
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

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
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 2500);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonServiceTestContext.CreateSession();
        var persistedDiagnosis = DaemonServiceTestContext.CreateDiagnosis();
        var diagnosis = DaemonServiceTestContext.CreateDiagnosis();
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(session, persistedDiagnosis),
        };
        var pingInfoClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Exception = new InvalidOperationException("daemon exited"),
        };
        var reachabilityClassifier = new DaemonServiceTestContext.StubDaemonReachabilityClassifier(static _ => true);
        var diagnosisResolver = new DaemonServiceTestContext.StubDaemonSessionDiagnosisResolver
        {
            Diagnosis = diagnosis,
        };
        var sessionMapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var diagnosisMapper = new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            reachabilityClassifier,
            diagnosisResolver,
            sessionMapper,
            diagnosisMapper);

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Stale, output.DaemonStatus);
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenStaleFallbackBudgetIsAlreadyExpired_ReturnsTimeoutBeforeDiagnosisResolution ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 250);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonServiceTestContext.CreateSession();
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(session),
        };
        var pingInfoClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Exception = new InvalidOperationException("daemon exited"),
            OnPingAndRead = () => timeProvider.Advance(TimeSpan.FromMilliseconds(250)),
        };
        var diagnosisResolver = new DaemonServiceTestContext.StubDaemonSessionDiagnosisResolver();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonServiceTestContext.StubDaemonReachabilityClassifier(static _ => true),
            diagnosisResolver,
            new DaemonServiceTestContext.StubDaemonSessionOutputMapper(),
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper(),
            timeProvider);

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: 250, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal("Timed out before stale daemon diagnosis could begin.", error.Message);
        Assert.Equal(0, diagnosisResolver.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenStaleFallbackDiagnosisResolutionTimesOut_ReturnsTimeoutFailure ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 250);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonServiceTestContext.CreateSession();
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(session),
        };
        var pingInfoClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Exception = new InvalidOperationException("daemon exited"),
        };
        var diagnosisStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnosisResolver = new DaemonServiceTestContext.StubDaemonSessionDiagnosisResolver
        {
            Handler = async (_, _, _, cancellationToken) =>
            {
                diagnosisStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                throw new System.Diagnostics.UnreachableException();
            },
        };
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonServiceTestContext.StubDaemonReachabilityClassifier(static _ => true),
            diagnosisResolver,
            new DaemonServiceTestContext.StubDaemonSessionOutputMapper(),
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper(),
            timeProvider);

        var resultTask = service.GetStatus(projectPath: null, timeoutMilliseconds: 250, cancellationToken: CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(diagnosisStarted.Task, "Daemon status stale diagnosis start", TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(250));

        var result = await TestAwaiter.WaitAsync(
            resultTask,
            "Daemon status stale diagnosis timeout result",
            TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal("Timed out while resolving stale daemon diagnosis.", error.Message);
        Assert.Equal(1, diagnosisResolver.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenStaleFallbackDiagnosisResolutionThrowsUnexpectedException_ReturnsInternalError ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 250);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonServiceTestContext.CreateSession();
        var daemonStatusOperation = new DaemonServiceTestContext.StubDaemonStatusOperation
        {
            StatusResult = DaemonStatusResult.Running(session),
        };
        var pingInfoClient = new DaemonServiceTestContext.StubDaemonPingInfoClient
        {
            Exception = new InvalidOperationException("daemon exited"),
        };
        var diagnosisResolver = new DaemonServiceTestContext.StubDaemonSessionDiagnosisResolver
        {
            Handler = static (_, _, _, _) => ValueTask.FromException<DaemonDiagnosis?>(new InvalidOperationException("diagnosis store failed")),
        };
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new DaemonServiceTestContext.StubDaemonReachabilityClassifier(static _ => true),
            diagnosisResolver,
            new DaemonServiceTestContext.StubDaemonSessionOutputMapper(),
            new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper());

        var result = await service.GetStatus(projectPath: null, timeoutMilliseconds: 250, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Failed to resolve stale daemon diagnosis. diagnosis store failed", error.Message);
        Assert.Equal(1, diagnosisResolver.CallCount);
    }

    private static DaemonStatusService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        IDaemonStatusOperation daemonStatusOperation,
        IDaemonSessionOutputMapper sessionOutputMapper,
        IDaemonDiagnosisOutputMapper diagnosisOutputMapper,
        TimeProvider? timeProvider = null)
    {
        return CreateService(
            resolver,
            daemonStatusOperation,
            new DaemonServiceTestContext.StubDaemonPingInfoClient(),
            new DaemonServiceTestContext.StubDaemonReachabilityClassifier(static _ => false),
            new DaemonServiceTestContext.StubDaemonSessionDiagnosisResolver(),
            sessionOutputMapper,
            diagnosisOutputMapper,
            timeProvider);
    }

    private static DaemonStatusService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        IDaemonStatusOperation daemonStatusOperation,
        IDaemonPingInfoClient pingInfoClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonSessionDiagnosisResolver diagnosisResolver,
        IDaemonSessionOutputMapper sessionOutputMapper,
        IDaemonDiagnosisOutputMapper diagnosisOutputMapper,
        TimeProvider? timeProvider = null)
    {
        return new DaemonStatusService(
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

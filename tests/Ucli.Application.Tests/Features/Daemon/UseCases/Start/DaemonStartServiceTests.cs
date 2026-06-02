using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartServiceTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsStarted_ReturnsRunningOutputWithMappedSession ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1200,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper
        {
            Output = DaemonServiceTestContext.CreateSessionOutput(),
        };
        var session = DaemonServiceTestContext.CreateSession();
        var lifecycleSnapshot = new DaemonStartLifecycleSnapshot(
            IpcEditorLifecycleStateCodec.Compiling,
            IpcEditorBlockingReasonCodec.Compile,
            CanAcceptExecutionRequests: false);
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.Started(session, lifecycleSnapshot),
        };
        var service = CreateService(resolver, supervisorProjectGateway, mapper);

        var result = await service.StartAsync(projectPath: null, timeoutMilliseconds: null, editorMode: null, onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStartExecutionOutput>(result.Output);
        Assert.Equal(DaemonStartStatus.Started, output.StartStatus);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(1200, output.TimeoutMilliseconds);
        Assert.Equal(mapper.Output, output.Session);
        Assert.Equal(IpcEditorLifecycleStateCodec.Compiling, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReasonCodec.Compile, output.BlockingReason);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Equal(1, supervisorProjectGateway.EnsureRunningCallCount);
        Assert.Equal(context.Context.UnityProject, supervisorProjectGateway.LastEnsureRunningUnityProject);
        Assert.True(supervisorProjectGateway.LastEnsureRunningTimeout > TimeSpan.Zero);
        Assert.True(supervisorProjectGateway.LastEnsureRunningTimeout <= context.Timeout);
        Assert.Null(supervisorProjectGateway.LastEnsureRunningSupervisorProgressObserver);
        Assert.Equal(1, mapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WithProgressSink_EmitsHostVisibleProgressInOrder ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1200,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var session = DaemonServiceTestContext.CreateSession();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningHandler = async (_, _, _, _, progressObserver, supervisorProgressObserver, cancellationToken) =>
            {
                Assert.NotNull(progressObserver);
                Assert.NotNull(supervisorProgressObserver);
                await progressObserver!.EmitSupervisorBootstrapStartedAsync(cancellationToken).ConfigureAwait(false);
                await progressObserver.EmitSupervisorBootstrapCompletedAsync(error: null, cancellationToken).ConfigureAwait(false);
                await progressObserver.EmitEnsureRunningStartedAsync(cancellationToken).ConfigureAwait(false);
                var startResult = DaemonStartResult.Started(session);
                await progressObserver.EmitEnsureRunningCompletedAsync(startResult, cancellationToken).ConfigureAwait(false);
                return startResult;
            },
        };
        var progressSink = new CollectingProgressSink();
        var service = CreateService(resolver, supervisorProjectGateway, mapper);

        var result = await service.StartAsync(
            projectPath: null,
            timeoutMilliseconds: null,
            editorMode: DaemonEditorMode.Batchmode,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink: progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, progressSink.Entries.Count);
        AssertProgressEvents(
            progressSink,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.EnsureRunningCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed));
        var startedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[0].Payload);
        Assert.Equal("fingerprint", startedEntry.ProjectFingerprint);
        Assert.Equal(1200, startedEntry.TimeoutMilliseconds);
        Assert.Equal("batchmode", startedEntry.EditorMode);
        Assert.Equal("auto", startedEntry.OnStartupBlocked);
        Assert.Null(startedEntry.Result);
        var completedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[^1].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(CommandProgressResult.Succeeded), completedEntry.Result);
        Assert.Equal("started", completedEntry.StartStatus);
        Assert.Equal("running", completedEntry.DaemonStatus);
        Assert.Null(completedEntry.ErrorCode);
        Assert.All(
            progressSink.Entries.Select(static entry => Assert.IsType<DaemonStartProgressEntry>(entry.Payload)),
            static entry => Assert.Equal(1200, entry.TimeoutMilliseconds));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenEditorModeIsSpecified_PropagatesEditorModeToLifecycleGateway ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1200,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.Started(DaemonServiceTestContext.CreateSession()),
        };
        var service = CreateService(resolver, supervisorProjectGateway, mapper);

        var result = await service.StartAsync(
            projectPath: null,
            timeoutMilliseconds: null,
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonEditorMode.Gui, supervisorProjectGateway.LastEnsureRunningEditorMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenOnStartupBlockedIsSpecified_PropagatesPolicyToLifecycleGateway ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1200,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.Started(DaemonServiceTestContext.CreateSession()),
        };
        var service = CreateService(resolver, supervisorProjectGateway, mapper);

        var result = await service.StartAsync(
            projectPath: null,
            timeoutMilliseconds: null,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Terminate,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartupBlockedProcessPolicy.Terminate, supervisorProjectGateway.LastEnsureRunningOnStartupBlocked);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExecutionContextResolutionFails_ReturnsFailureWithoutSupervisorCall ()
    {
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(
                ExecutionError.InvalidArgument("invalid project path")));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway();
        var progressSink = new CollectingProgressSink();
        var service = CreateService(resolver, supervisorProjectGateway, mapper);

        var result = await service.StartAsync(
            projectPath: null,
            timeoutMilliseconds: null,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink: progressSink,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(0, supervisorProjectGateway.EnsureRunningCallCount);
        Assert.Equal(0, mapper.CallCount);
        Assert.Empty(progressSink.Entries);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsAlreadyRunning_PreservesAlreadyRunningStatus ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1200,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.AlreadyRunning(DaemonServiceTestContext.CreateSession()),
        };
        var service = CreateService(resolver, supervisorProjectGateway, mapper);

        var result = await service.StartAsync(projectPath: null, timeoutMilliseconds: null, editorMode: null, onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStartExecutionOutput>(result.Output);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, output.StartStatus);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsAttached_PreservesAttachedStatus ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1200,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.Attached(DaemonServiceTestContext.CreateSession()),
        };
        var service = CreateService(resolver, supervisorProjectGateway, mapper);

        var result = await service.StartAsync(projectPath: null, timeoutMilliseconds: null, editorMode: null, onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStartExecutionOutput>(result.Output);
        Assert.Equal(DaemonStartStatus.Attached, output.StartStatus);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsFailure_ReturnsFailure ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1600,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.Failure(ExecutionError.Timeout("start failed")),
        };
        var progressSink = new CollectingProgressSink();
        var service = CreateService(resolver, supervisorProjectGateway, mapper);

        var result = await service.StartAsync(
            projectPath: null,
            timeoutMilliseconds: null,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink: progressSink,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal("start failed", error.Message);
        Assert.Equal(0, mapper.CallCount);
        AssertProgressEvents(
            progressSink,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed));
        var completedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[^1].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(CommandProgressResult.Failed), completedEntry.Result);
        Assert.Equal("failed", completedEntry.StartStatus);
        Assert.Equal("notRunning", completedEntry.DaemonStatus);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout.Value, completedEntry.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsFailureAndDiagnosisExists_AttachesMappedDiagnosis ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1600,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.Failure(
                ExecutionError.Timeout("start failed", ExecutionErrorCodes.IpcTimeout),
                DaemonServiceTestContext.CreateDiagnosis() with
                {
                    Reason = DaemonDiagnosisReasonValues.GuiEndpointNotRegistered,
                    EditorInstancePath = "/tmp/unity-project/Library/EditorInstance.json",
                }),
        };
        var diagnosis = supervisorProjectGateway.EnsureRunningResult.Diagnosis!;
        var diagnosisOutputMapper = new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper
        {
            Output = new(
                Reason: DaemonDiagnosisReasonValues.GuiEndpointNotRegistered,
                Message: "mapped diagnosis",
                ReportedBy: DaemonDiagnosisReportedByValues.Cli,
                IsInferred: true,
                UpdatedAtUtc: diagnosis.UpdatedAtUtc,
                ProcessId: diagnosis.ProcessId,
                EditorInstancePath: diagnosis.EditorInstancePath),
        };
        var service = CreateService(
            resolver,
            supervisorProjectGateway,
            mapper,
            daemonDiagnosisOutputMapper: diagnosisOutputMapper);

        var result = await service.StartAsync(projectPath: null, timeoutMilliseconds: null, editorMode: null, onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        var failureOutput = Assert.IsType<DaemonStartFailureExecutionOutput>(result.FailureOutput);
        Assert.Equal(DaemonStatusKind.NotRunning, failureOutput.DaemonStatus);
        Assert.Equal(1600, failureOutput.TimeoutMilliseconds);
        Assert.Equal(diagnosisOutputMapper.Output, failureOutput.Diagnosis);
        Assert.Equal(1, diagnosisOutputMapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsFailureWithStartup_PreservesFailurePayloadFields ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 1600,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var startup = new DaemonStartupObservation(
            StartupStatus: DaemonStartupStatusValues.Blocked,
            StartupBlockingReason: DaemonStartupBlockingReasonValues.Compile,
            LaunchAttemptId: "20260312_040500Z_00abcdef",
            ProcessAction: DaemonStartupProcessActionValues.Kept,
            RetryDisposition: DaemonStartupRetryDispositionValues.RetryAfterFix);
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.Failure(
                ExecutionError.InternalError("startup blocked", DaemonErrorCodes.DaemonStartupBlocked),
                DaemonServiceTestContext.CreateDiagnosis(),
                startup,
                DaemonStatusKind.Stale),
        };
        var diagnosisOutputMapper = new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper();
        var service = CreateService(
            resolver,
            supervisorProjectGateway,
            mapper,
            daemonDiagnosisOutputMapper: diagnosisOutputMapper);

        var result = await service.StartAsync(
            projectPath: null,
            timeoutMilliseconds: null,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.Error!.Code);
        var failureOutput = Assert.IsType<DaemonStartFailureExecutionOutput>(result.FailureOutput);
        Assert.Equal(DaemonStatusKind.Stale, failureOutput.DaemonStatus);
        Assert.Equal(1600, failureOutput.TimeoutMilliseconds);
        Assert.Equal(startup.StartupBlockingReason, failureOutput.Startup!.StartupBlockingReason);
        Assert.Equal(startup.RetryDisposition, failureOutput.Startup.RetryDisposition);
        Assert.Equal(diagnosisOutputMapper.Output, failureOutput.Diagnosis);
        Assert.Equal(DaemonStartupRetryDispositionValues.RetryAfterFix, failureOutput.RetryDisposition);
        Assert.False(failureOutput.SafeToRetryImmediately);
        Assert.Equal(1, diagnosisOutputMapper.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenPluginVerificationConsumesBudget_PropagatesRemainingTimeoutToEnsureRunning ()
    {
        var timeProvider = new ManualTimeProvider();

        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 700,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.Started(DaemonServiceTestContext.CreateSession()),
        };
        var pluginVerifier = new StubUnityPluginVerifier
        {
            Handler = cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                return ValueTask.FromResult(UnityPluginVerificationResult.Success());
            },
        };
        var service = CreateService(resolver, supervisorProjectGateway, mapper, pluginVerifier, timeProvider: timeProvider);

        var result = await service.StartAsync(
            projectPath: "/tmp/sandbox-unity",
            timeoutMilliseconds: 700,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UcliCommandIds.DaemonStart, resolver.LastTimeoutCommand);
        Assert.Equal("/tmp/sandbox-unity", resolver.LastProjectPath);
        Assert.Equal(700, resolver.LastTimeoutMilliseconds);

        Assert.Equal(1, supervisorProjectGateway.EnsureRunningCallCount);
        Assert.Equal(TimeSpan.FromMilliseconds(500), supervisorProjectGateway.LastEnsureRunningTimeout);
        Assert.Equal(context.Context.UnityProject, supervisorProjectGateway.LastEnsureRunningUnityProject);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenProgressSinkConsumesTime_DoesNotConsumeTimeoutBudget ()
    {
        var timeProvider = new ManualTimeProvider();

        var context = DaemonServiceTestContext.CreateExecutionContext(
            timeoutMilliseconds: 700,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway
        {
            EnsureRunningResult = DaemonStartResult.Started(DaemonServiceTestContext.CreateSession()),
        };
        var pluginVerifier = new StubUnityPluginVerifier
        {
            Handler = cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                return ValueTask.FromResult(UnityPluginVerificationResult.Success());
            },
        };
        var progressSink = new CollectingProgressSink(() => timeProvider.Advance(TimeSpan.FromMilliseconds(250)));
        var service = CreateService(resolver, supervisorProjectGateway, mapper, pluginVerifier, timeProvider: timeProvider);

        var result = await service.StartAsync(
            projectPath: "/tmp/sandbox-unity",
            timeoutMilliseconds: 700,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink: progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, supervisorProjectGateway.EnsureRunningCallCount);
        Assert.Equal(TimeSpan.FromMilliseconds(500), supervisorProjectGateway.LastEnsureRunningTimeout);
        AssertProgressEvents(
            progressSink,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenUnityPluginMarkerIsMissing_ReturnsInvalidArgumentBeforeSupervisorBootstrap ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 1200);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var pluginVerifier = new StubUnityPluginVerifier
        {
            Result = UnityPluginVerificationResult.Failure(ExecutionError.InvalidArgument(
                "Unity project does not contain the uCLI Unity plugin.")),
        };
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway();
        var progressSink = new CollectingProgressSink();
        var service = CreateService(resolver, supervisorProjectGateway, mapper, pluginVerifier);

        var result = await service.StartAsync(
            projectPath: null,
            timeoutMilliseconds: null,
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink: progressSink,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(1, pluginVerifier.CallCount);
        Assert.Equal(0, supervisorProjectGateway.EnsureRunningCallCount);
        Assert.Equal(0, mapper.CallCount);
        AssertProgressEvents(
            progressSink,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed));
        var pluginCompletedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[2].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(CommandProgressResult.Failed), pluginCompletedEntry.Result);
        Assert.Equal("INVALID_ARGUMENT", pluginCompletedEntry.ErrorCode);
        var completedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[^1].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(CommandProgressResult.Failed), completedEntry.Result);
        Assert.Equal("failed", completedEntry.StartStatus);
        Assert.Equal("notRunning", completedEntry.DaemonStatus);
        Assert.Equal("INVALID_ARGUMENT", completedEntry.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenUnityPluginVerificationExceedsTimeout_ReturnsTimeoutBeforeSupervisorBootstrap ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 120);
        var timeProvider = new ManualTimeProvider();
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var mapper = new DaemonServiceTestContext.StubDaemonSessionOutputMapper();
        var supervisorProjectGateway = new DaemonServiceTestContext.StubSupervisorProjectGateway();
        var pluginVerifier = new StubUnityPluginVerifier
        {
            Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            Handler = async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return UnityPluginVerificationResult.Success();
            },
        };
        var service = CreateService(resolver, supervisorProjectGateway, mapper, pluginVerifier, timeProvider: timeProvider);

        var resultTask = service.StartAsync(projectPath: null, timeoutMilliseconds: null, editorMode: null, onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto, cancellationToken: CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(pluginVerifier.Started!.Task, "Unity plugin verification start", SignalWaitTimeout);
        timeProvider.Advance(context.Timeout);

        var result = await TestAwaiter.WaitAsync(resultTask, "Unity plugin verification timeout result", SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.True(pluginVerifier.ObservedCancellation);
        Assert.Equal(0, supervisorProjectGateway.EnsureRunningCallCount);
        Assert.Equal(0, mapper.CallCount);
    }

    private static DaemonStartService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        DaemonServiceTestContext.StubSupervisorProjectGateway supervisorProjectGateway,
        IDaemonSessionOutputMapper mapper,
        IUnityPluginVerifier? pluginVerifier = null,
        IDaemonDiagnosisOutputMapper? daemonDiagnosisOutputMapper = null,
        TimeProvider? timeProvider = null)
    {
        pluginVerifier ??= new StubUnityPluginVerifier();
        return new DaemonStartService(
            resolver,
            supervisorProjectGateway,
            pluginVerifier,
            mapper,
            daemonDiagnosisOutputMapper ?? new DaemonServiceTestContext.StubDaemonDiagnosisOutputMapper(),
            timeProvider);
    }

    private static void AssertProgressEvents (
        CollectingProgressSink progressSink,
        params string[] eventNames)
    {
        Assert.Equal(eventNames.Length, progressSink.Entries.Count);
        for (var i = 0; i < eventNames.Length; i++)
        {
            Assert.Equal(eventNames[i], progressSink.Entries[i].EventName);
        }
    }

    private sealed class CollectingProgressSink : ICommandProgressSink
    {
        private readonly List<ProgressEntry> entries = [];
        private readonly Action? onEntry;

        public CollectingProgressSink (Action? onEntry = null)
        {
            this.onEntry = onEntry;
        }

        public IReadOnlyList<ProgressEntry> Entries => entries;

        public ValueTask OnEntryAsync<TPayload> (
            string eventName,
            TPayload payload,
            CancellationToken cancellationToken = default)
            where TPayload : notnull
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(new ProgressEntry(eventName, payload));
            onEntry?.Invoke();
            return ValueTask.CompletedTask;
        }
    }

    private sealed record ProgressEntry (
        string EventName,
        object Payload);

    private sealed class StubUnityPluginVerifier : IUnityPluginVerifier
    {
        public int CallCount { get; private set; }

        public Func<CancellationToken, ValueTask<UnityPluginVerificationResult>>? Handler { get; set; }

        public bool ObservedCancellation { get; private set; }

        public TaskCompletionSource? Started { get; set; }

        public UnityPluginVerificationResult Result { get; set; } = UnityPluginVerificationResult.Success();

        public string? LastUnityProjectRoot { get; private set; }

        public ValueTask<UnityPluginVerificationResult> VerifyAsync (
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

            return LocateCoreAsync(cancellationToken);
        }

        private async ValueTask<UnityPluginVerificationResult> LocateCoreAsync (CancellationToken cancellationToken)
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

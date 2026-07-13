using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartServiceFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExecutionContextResolutionFails_ReturnsFailureWithoutSupervisorCall ()
    {
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(
                ExecutionError.InvalidArgument("invalid project path")));
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway();
        var progressSink = new CollectingCommandProgressSink();
        var service = DaemonStartServiceTestSupport.CreateService(resolver, supervisorProjectGateway);

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
        DaemonStartServiceAssert.StartNotAttemptedAfterContextResolutionFailure(
            supervisorProjectGateway,
            progressSink);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsFailure_ReturnsFailure ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(
            timeoutMilliseconds: 1600,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            EnsureRunningResult = DaemonStartResult.Failure(ExecutionError.Timeout("start failed")),
        };
        var progressSink = new CollectingCommandProgressSink();
        var service = DaemonStartServiceTestSupport.CreateService(resolver, supervisorProjectGateway);

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
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Started),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationStarted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.PluginVerificationCompleted),
            ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed));
        DaemonStartProgressAssert.CompletedWithStartupFailure(
            progressSink,
            ExecutionErrorCodes.IpcTimeout.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsFailureAndDiagnosisExists_AttachesMappedDiagnosis ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(
            timeoutMilliseconds: 1600,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            EnsureRunningResult = DaemonStartResult.Failure(
                ExecutionError.Timeout("start failed", ExecutionErrorCodes.IpcTimeout),
                DaemonDiagnosisTestFactory.Create() with
                {
                    Reason = DaemonDiagnosisReasonValues.GuiEndpointNotRegistered,
                    EditorInstancePath = "/tmp/unity-project/Library/EditorInstance.json",
                }),
        };
        var diagnosis = supervisorProjectGateway.EnsureRunningResult.Diagnosis!;
        var service = DaemonStartServiceTestSupport.CreateService(resolver, supervisorProjectGateway);

        var result = await service.StartAsync(projectPath: null, timeoutMilliseconds: null, editorMode: null, onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        var failureOutput = Assert.IsType<DaemonStartFailureExecutionOutput>(result.FailureOutput);
        Assert.Equal(DaemonStatusKind.NotRunning, failureOutput.DaemonStatus);
        Assert.Equal(1600, failureOutput.TimeoutMilliseconds);
        DaemonServiceOutputAssert.DiagnosisMatches(diagnosis, failureOutput.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsFailureWithStartup_PreservesFailurePayloadFields ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(
            timeoutMilliseconds: 1600,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var startup = new DaemonStartupObservation(
            StartupStatus: DaemonStartupStatus.Blocked,
            StartupBlockingReason: DaemonStartupBlockingReason.Compile,
            LaunchAttemptId: "20260312_040500Z_00abcdef",
            ProcessAction: DaemonStartupProcessAction.Kept,
            RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix);
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            EnsureRunningResult = DaemonStartResult.Failure(
                ExecutionError.InternalError("startup blocked", DaemonErrorCodes.DaemonStartupBlocked),
                DaemonDiagnosisTestFactory.Create(),
                startup,
                DaemonStatusKind.Stale),
        };
        var diagnosis = supervisorProjectGateway.EnsureRunningResult.Diagnosis!;
        var service = DaemonStartServiceTestSupport.CreateService(resolver, supervisorProjectGateway);

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
        DaemonServiceOutputAssert.DiagnosisMatches(diagnosis, failureOutput.Diagnosis);
        Assert.Equal(DaemonStartupRetryDisposition.RetryAfterFix, failureOutput.RetryDisposition);
        Assert.False(failureOutput.SafeToRetryImmediately);
    }
}

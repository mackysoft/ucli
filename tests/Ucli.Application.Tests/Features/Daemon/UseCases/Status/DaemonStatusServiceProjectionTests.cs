using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStatusServiceProjectionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDiagnosisExists_MapsDiagnosisToOutput ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2400);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var diagnosis = DaemonDiagnosisTestFactory.Create();
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.NotRunning(diagnosis));
        var service = CreateService(
            resolver,
            daemonStatusOperation);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.NotNull(output.Diagnosis);
        DaemonServiceOutputAssert.DiagnosisMatches(diagnosis, output.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenLastLaunchAttemptExists_MapsLastLaunchAttemptToOutput ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2410);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var diagnosis = DaemonDiagnosisTestFactory.Create();
        var launchAttempt = CreateTimedOutLaunchAttempt(diagnosis);
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.NotRunning(diagnosis: null, lastLaunchAttempt: launchAttempt));
        var service = CreateService(
            resolver,
            daemonStatusOperation);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        var actual = Assert.IsType<DaemonLaunchAttemptOutput>(output.LastLaunchAttempt);
        Assert.Equal(launchAttempt.LaunchAttemptId, actual.LaunchAttemptId);
        Assert.Equal(launchAttempt.StartupStatus, actual.StartupStatus);
        Assert.Equal(launchAttempt.StartupBlockingReason, actual.StartupBlockingReason);
        Assert.Equal(launchAttempt.RetryDisposition, actual.RetryDisposition);
        Assert.Equal(launchAttempt.ProcessAction, actual.ProcessAction);
        Assert.Equal(launchAttempt.ArtifactPath, actual.ArtifactPath);
        Assert.Equal(launchAttempt.UnityLogPath, actual.UnityLogPath);
        Assert.Equal(launchAttempt.UpdatedAtUtc, actual.UpdatedAtUtc);
        Assert.Equal(launchAttempt.ProcessId, actual.ProcessId);
        Assert.Equal(launchAttempt.ProcessStartedAtUtc, actual.ProcessStartedAtUtc);
        DaemonServiceOutputAssert.DiagnosisMatches(diagnosis, actual.Diagnosis);
    }

    private static DaemonLaunchAttempt CreateTimedOutLaunchAttempt (DaemonDiagnosis diagnosis)
    {
        return new DaemonLaunchAttempt(
            LaunchAttemptId: "20260312_000000Z_00000001",
            StartedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 5, TimeSpan.Zero),
            StartupStatus: DaemonStartupStatus.Timeout,
            StartupBlockingReason: DaemonStartupBlockingReason.EndpointNotRegistered,
            RetryDisposition: DaemonStartupRetryDisposition.WaitThenRetry,
            ProcessAction: DaemonStartupProcessAction.Terminated,
            EditorMode: DaemonEditorMode.Gui,
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 1, TimeSpan.Zero),
            UnityLogPath: "/tmp/repo-root/.ucli/local/fingerprints/fingerprint/unity.log",
            ArtifactPath: "/tmp/repo-root/.ucli/local/fingerprints/fingerprint/launch-attempts/20260312_000000Z_00000001/startup-diagnosis.json",
            Diagnosis: diagnosis);
    }
}

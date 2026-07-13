using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonGuiEditorAttachServiceDiagnosisTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenEndpointRegistrationTimesOut_WritesGuiEndpointDiagnosis ()
    {
        var marker = DaemonGuiEditorAttachServiceTestSupport.CreateMarker();
        var markerReader = new RecordingUnityEditorInstanceMarkerReader
        {
            ReadResult = UnityEditorInstanceMarkerReadResult.Success(marker),
        };
        var processProbe = new RecordingUnityGuiEditorProcessProbe
        {
            Result = UnityGuiEditorProcessProbeResult.Matching(DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc),
        };
        var timeoutError = ExecutionError.Timeout("wait timed out", ExecutionErrorCodes.IpcTimeout);
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter
        {
            Result = DaemonGuiSessionRegistrationWaitResult.Failure(timeoutError),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var rebootstrapClient = new RecordingDaemonGuiRebootstrapClient();
        var service = new DaemonGuiEditorAttachService(markerReader, processProbe, awaiter, rebootstrapClient, diagnosisStore);
        var progressObserver = new CollectingDaemonStartProgressObserver();

        var result = await service.TryAttachExistingGuiEditorAsync(
            DaemonGuiEditorAttachServiceTestSupport.UnityProject,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            DaemonStartupBlockedProcessPolicy.Terminate,
            progressObserver,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error.Code);
        DaemonGuiAttachInvocationAssert.RebootstrapRequestedFor(
            rebootstrapClient,
            DaemonGuiEditorAttachServiceTestSupport.UnityProject,
            marker.ProcessId,
            DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc);
        var diagnosis = DaemonDiagnosisStoreAssert.WrittenOnceWithReason(
            diagnosisStore,
            DaemonDiagnosisReasonValues.GuiEndpointNotRegistered);
        Assert.Equal(DaemonDiagnosisReportedByValues.Cli, diagnosis.ReportedBy);
        Assert.True(diagnosis.IsInferred);
        Assert.Equal(marker.ProcessId, diagnosis.ProcessId);
        Assert.Equal(marker.MarkerPath, diagnosis.EditorInstancePath);
        Assert.NotNull(result.Startup);
        Assert.Equal(DaemonEditorMode.Gui, result.Startup!.EditorMode);
        Assert.Equal(DaemonSessionOwnerKind.User, result.Startup.OwnerKind);
        Assert.False(result.Startup.CanShutdownProcess);
        Assert.Equal(marker.ProcessId, result.Startup.ProcessId);
        Assert.Equal(DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc, result.Startup.StartedAtUtc);
        Assert.Equal(DaemonStartupProcessAction.Kept, result.Startup.ProcessAction);
        progressObserver.AssertEvents(
            DaemonStartProgressEvent.WaitingForEndpoint,
            DaemonStartProgressEvent.BlockerDetected);
        var blockerObservation = progressObserver.PayloadAt<DaemonStartStartupProgressObservation>(1);
        Assert.Equal(DaemonStartupStatus.Timeout, blockerObservation.StartupStatus);
        Assert.Equal(DaemonStartupBlockingReason.EndpointNotRegistered, blockerObservation.StartupBlockingReason);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout.Value, blockerObservation.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenRebootstrapUnavailable_WritesGuiRebootstrapDiagnosis ()
    {
        var marker = DaemonGuiEditorAttachServiceTestSupport.CreateMarker();
        var markerReader = new RecordingUnityEditorInstanceMarkerReader
        {
            ReadResult = UnityEditorInstanceMarkerReadResult.Success(marker),
        };
        var processProbe = new RecordingUnityGuiEditorProcessProbe
        {
            Result = UnityGuiEditorProcessProbeResult.Matching(DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc),
        };
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter
        {
            Result = DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("session missing")),
        };
        var rebootstrapClient = new RecordingDaemonGuiRebootstrapClient
        {
            Result = DaemonGuiRebootstrapRequestResult.Unavailable(ExecutionError.InternalError(
                "manifest missing",
                DaemonErrorCodes.DaemonEndpointNotRegistered)),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = new DaemonGuiEditorAttachService(
            markerReader,
            processProbe,
            awaiter,
            rebootstrapClient,
            diagnosisStore);
        var progressObserver = new CollectingDaemonStartProgressObserver();

        var result = await service.TryAttachExistingGuiEditorAsync(
            DaemonGuiEditorAttachServiceTestSupport.UnityProject,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            DaemonStartupBlockedProcessPolicy.Terminate,
            progressObserver,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonEndpointNotRegistered, result.Error!.Code);
        DaemonGuiAttachInvocationAssert.EndpointWaitAttemptedFor(
            awaiter,
            DaemonGuiEditorAttachServiceTestSupport.UnityProject,
            marker.ProcessId,
            DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc);
        DaemonGuiAttachInvocationAssert.RebootstrapRequestedFor(
            rebootstrapClient,
            DaemonGuiEditorAttachServiceTestSupport.UnityProject,
            marker.ProcessId,
            DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc);
        DaemonDiagnosisStoreAssert.WrittenOnceWithReason(
            diagnosisStore,
            DaemonDiagnosisReasonValues.GuiRebootstrapUnavailable);
        Assert.Equal(DaemonStartupProcessAction.Kept, result.Startup!.ProcessAction);
        progressObserver.AssertEvents(
            DaemonStartProgressEvent.WaitingForEndpoint,
            DaemonStartProgressEvent.BlockerDetected);
        var blockerObservation = progressObserver.PayloadAt<DaemonStartStartupProgressObservation>(1);
        Assert.Equal(DaemonStartupStatus.Failed, blockerObservation.StartupStatus);
        Assert.Equal(DaemonStartupBlockingReason.EndpointNotRegistered, blockerObservation.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDisposition.Unknown, blockerObservation.RetryDisposition);
        Assert.Equal(DaemonErrorCodes.DaemonEndpointNotRegistered.Value, blockerObservation.ErrorCode);
    }
}

using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonGuiEditorAttachServiceTimeoutBudgetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenMarkerAndProbeConsumeTime_PassesRemainingTimeoutToAwaiter ()
    {
        var timeProvider = new ManualTimeProvider();
        var marker = DaemonGuiEditorAttachServiceTestSupport.CreateMarker();
        var markerReader = new RecordingUnityEditorInstanceMarkerReader
        {
            ReadResult = UnityEditorInstanceMarkerReadResult.Success(marker),
            OnRead = () => timeProvider.Advance(TimeSpan.FromMilliseconds(125)),
        };
        var processProbe = new RecordingUnityGuiEditorProcessProbe
        {
            Result = UnityGuiEditorProcessProbeResult.Matching(DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc),
            OnProbe = () => timeProvider.Advance(TimeSpan.FromMilliseconds(175)),
        };
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter();
        var service = new DaemonGuiEditorAttachService(
            markerReader,
            processProbe,
            awaiter,
            new RecordingDaemonGuiRebootstrapClient(),
            new RecordingDaemonDiagnosisStore(),
            timeProvider);

        var result = await service.TryAttachExistingGuiEditorAsync(
            DaemonGuiEditorAttachServiceTestSupport.UnityProject,
            TimeSpan.FromMilliseconds(1000),
            editorMode: null,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess);
        DaemonGuiAttachInvocationAssert.EndpointWaitAttemptedFor(
            awaiter,
            DaemonGuiEditorAttachServiceTestSupport.UnityProject,
            marker.ProcessId,
            DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(175));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenRebootstrapPathConsumesTime_PassesRemainingTimeouts ()
    {
        var timeProvider = new ManualTimeProvider();
        var marker = DaemonGuiEditorAttachServiceTestSupport.CreateMarker();
        var markerReader = new RecordingUnityEditorInstanceMarkerReader
        {
            ReadResult = UnityEditorInstanceMarkerReadResult.Success(marker),
            OnRead = () => timeProvider.Advance(TimeSpan.FromMilliseconds(100)),
        };
        var processProbe = new RecordingUnityGuiEditorProcessProbe
        {
            Result = UnityGuiEditorProcessProbeResult.Matching(DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc),
            OnProbe = () => timeProvider.Advance(TimeSpan.FromMilliseconds(100)),
        };
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter();
        awaiter.AdvanceTimeOnFirstWait(timeProvider, TimeSpan.FromMilliseconds(200));
        awaiter.Results.Enqueue(DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("session missing")));
        awaiter.Results.Enqueue(DaemonGuiSessionRegistrationWaitResult.Success(
            DaemonGuiEditorAttachServiceTestSupport.CreateGuiSession(),
            DaemonGuiEditorAttachServiceTestSupport.CreateReadyLifecycleObservation()));
        var rebootstrapClient = new RecordingDaemonGuiRebootstrapClient
        {
            OnRequest = () => timeProvider.Advance(TimeSpan.FromMilliseconds(150)),
        };
        var service = new DaemonGuiEditorAttachService(
            markerReader,
            processProbe,
            awaiter,
            rebootstrapClient,
            new RecordingDaemonDiagnosisStore(),
            timeProvider);

        var result = await service.TryAttachExistingGuiEditorAsync(
            DaemonGuiEditorAttachServiceTestSupport.UnityProject,
            TimeSpan.FromMilliseconds(1000),
            editorMode: null,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess);
        DaemonGuiAttachInvocationAssert.EndpointWaitsUsedTimeouts(
            awaiter,
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(450));
        DaemonGuiAttachInvocationAssert.RebootstrapRequestedFor(
            rebootstrapClient,
            DaemonGuiEditorAttachServiceTestSupport.UnityProject,
            marker.ProcessId,
            DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(600));
    }
}

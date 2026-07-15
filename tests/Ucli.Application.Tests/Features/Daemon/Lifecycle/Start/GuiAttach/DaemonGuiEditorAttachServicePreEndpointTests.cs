using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonGuiEditorAttachServicePreEndpointTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenBatchmodeRequestedAndGuiMarkerMatches_ReturnsEditorModeMismatch ()
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
        var awaiter = new UnexpectedDaemonGuiSessionRegistrationAwaiter("Batchmode editor mode mismatch should stop before endpoint wait.");
        var diagnosisStore = new UnexpectedDaemonDiagnosisStore("Batchmode editor mode mismatch should not write diagnosis.");
        var service = new DaemonGuiEditorAttachService(
            markerReader,
            processProbe,
            awaiter,
            new UnexpectedDaemonGuiRebootstrapClient("Batchmode editor mode mismatch should not request rebootstrap."),
            diagnosisStore,
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());

        var result = await service.TryAttachExistingGuiEditorAsync(
            DaemonGuiEditorAttachServiceTestSupport.UnityProject,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new ManualTimeProvider()),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonEditorModeMismatch, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenProcessProbeRejectsMarker_ReturnsNullWithoutWaiting ()
    {
        var markerReader = new RecordingUnityEditorInstanceMarkerReader
        {
            ReadResult = UnityEditorInstanceMarkerReadResult.Success(DaemonGuiEditorAttachServiceTestSupport.CreateMarker()),
        };
        var processProbe = new RecordingUnityGuiEditorProcessProbe
        {
            Result = UnityGuiEditorProcessProbeResult.NotMatching(UnityGuiEditorProcessProbeStatus.NotUnityEditor),
        };
        var awaiter = new UnexpectedDaemonGuiSessionRegistrationAwaiter("Rejected GUI marker should stop before endpoint wait.");
        var service = new DaemonGuiEditorAttachService(
            markerReader,
            processProbe,
            awaiter,
            new UnexpectedDaemonGuiRebootstrapClient("Rejected GUI marker should not request rebootstrap."),
            new UnexpectedDaemonDiagnosisStore("Rejected GUI marker should not write diagnosis."),
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());

        var result = await service.TryAttachExistingGuiEditorAsync(
            DaemonGuiEditorAttachServiceTestSupport.UnityProject,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new ManualTimeProvider()),
            editorMode: null,
            DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
    }
}

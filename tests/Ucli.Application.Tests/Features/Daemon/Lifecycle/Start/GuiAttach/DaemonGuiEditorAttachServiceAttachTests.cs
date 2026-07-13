using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonGuiEditorAttachServiceAttachTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenMatchingGuiSessionRegisters_ReturnsAttached ()
    {
        var marker = DaemonGuiEditorAttachServiceTestSupport.CreateMarker();
        var context = DaemonGuiEditorAttachServiceTestSupport.UnityProject;
        var markerReader = new RecordingUnityEditorInstanceMarkerReader
        {
            ReadResult = UnityEditorInstanceMarkerReadResult.Success(marker),
        };
        var processProbe = new RecordingUnityGuiEditorProcessProbe
        {
            Result = UnityGuiEditorProcessProbeResult.Matching(DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc),
        };
        var session = DaemonGuiEditorAttachServiceTestSupport.CreateGuiSession();
        var lifecycleObservation = DaemonGuiEditorAttachServiceTestSupport.CreateReadyLifecycleObservation();
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter
        {
            Result = DaemonGuiSessionRegistrationWaitResult.Success(session, lifecycleObservation),
        };
        var diagnosisStore = new UnexpectedDaemonDiagnosisStore("Initial GUI attach success should not write diagnosis.");
        var rebootstrapClient = new UnexpectedDaemonGuiRebootstrapClient("Initial GUI attach success should not request rebootstrap.");
        var service = new DaemonGuiEditorAttachService(
            markerReader,
            processProbe,
            awaiter,
            rebootstrapClient,
            diagnosisStore,
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());
        var progressObserver = new CollectingDaemonStartProgressObserver();

        var result = await service.TryAttachExistingGuiEditorAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            DaemonStartupBlockedProcessPolicy.Auto,
            progressObserver,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess);
        Assert.Equal(DaemonStartStatus.Attached, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(lifecycleObservation, result.LifecycleObservation);
        progressObserver.AssertEvents(
            DaemonStartProgressEvent.WaitingForEndpoint,
            DaemonStartProgressEvent.SessionRegistered,
            DaemonStartProgressEvent.EndpointRegistered,
            DaemonStartProgressEvent.LifecycleObserved);
        Assert.Equal(marker.ProcessId, progressObserver.PayloadAt<DaemonStartStartupProgressObservation>(0).ProcessId);
        Assert.Equal(session, progressObserver.PayloadAt<DaemonSession>(1));
        Assert.Equal(session, progressObserver.PayloadAt<DaemonSession>(2));
        Assert.Equal(lifecycleObservation, progressObserver.PayloadAt<IpcUnityEditorObservation>(3));
        DaemonGuiAttachInvocationAssert.EndpointWaitAttemptedFor(
            awaiter,
            context,
            marker.ProcessId,
            DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(125));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenRebootstrapAcceptedAndSessionRegisters_ReturnsAttached ()
    {
        var marker = DaemonGuiEditorAttachServiceTestSupport.CreateMarker();
        var context = DaemonGuiEditorAttachServiceTestSupport.UnityProject;
        var markerReader = new RecordingUnityEditorInstanceMarkerReader
        {
            ReadResult = UnityEditorInstanceMarkerReadResult.Success(marker),
        };
        var processProbe = new RecordingUnityGuiEditorProcessProbe
        {
            Result = UnityGuiEditorProcessProbeResult.Matching(DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc),
        };
        var session = DaemonGuiEditorAttachServiceTestSupport.CreateGuiSession();
        var lifecycleObservation = DaemonGuiEditorAttachServiceTestSupport.CreateReadyLifecycleObservation();
        var awaiter = new RecordingDaemonGuiSessionRegistrationAwaiter();
        awaiter.Results.Enqueue(DaemonGuiSessionRegistrationWaitResult.Failure(ExecutionError.Timeout("session missing")));
        awaiter.Results.Enqueue(DaemonGuiSessionRegistrationWaitResult.Success(session, lifecycleObservation));
        var diagnosisStore = new UnexpectedDaemonDiagnosisStore("Successful GUI rebootstrap attach should not write diagnosis.");
        var rebootstrapClient = new RecordingDaemonGuiRebootstrapClient();
        var service = new DaemonGuiEditorAttachService(
            markerReader,
            processProbe,
            awaiter,
            rebootstrapClient,
            diagnosisStore,
            new DaemonCompensationOperationOwner(),
            new ManualTimeProvider());
        var progressObserver = new CollectingDaemonStartProgressObserver();

        var result = await service.TryAttachExistingGuiEditorAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            DaemonStartupBlockedProcessPolicy.Terminate,
            progressObserver,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSuccess);
        Assert.Equal(DaemonStartStatus.Attached, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(DaemonEditorMode.Gui, result.Session!.EditorMode);
        Assert.Equal(DaemonSessionOwnerKind.User, result.Session.OwnerKind);
        Assert.False(result.Session.CanShutdownProcess);
        Assert.Equal(marker.ProcessId, result.Session.ProcessId);
        Assert.Equal(lifecycleObservation, result.LifecycleObservation);
        progressObserver.AssertEvents(
            DaemonStartProgressEvent.WaitingForEndpoint,
            DaemonStartProgressEvent.SessionRegistered,
            DaemonStartProgressEvent.EndpointRegistered,
            DaemonStartProgressEvent.LifecycleObserved);
        Assert.Equal(marker.ProcessId, progressObserver.PayloadAt<DaemonStartStartupProgressObservation>(0).ProcessId);
        Assert.Equal(session, progressObserver.PayloadAt<DaemonSession>(1));
        Assert.Equal(session, progressObserver.PayloadAt<DaemonSession>(2));
        Assert.Equal(lifecycleObservation, progressObserver.PayloadAt<IpcUnityEditorObservation>(3));
        DaemonGuiAttachInvocationAssert.RebootstrapRequestedFor(
            rebootstrapClient,
            context,
            marker.ProcessId,
            DaemonGuiEditorAttachServiceTestSupport.ProbeProcessStartedAtUtc);
    }
}

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonGuiEditorAttachServiceTimeoutBudgetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenMarkerReadIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCompletion = new TaskCompletionSource<UnityEditorInstanceMarkerReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var markerReader = new RecordingUnityEditorInstanceMarkerReader
        {
            ReadAsyncHandler = (_, _) =>
            {
                readStarted.TrySetResult();
                return new ValueTask<UnityEditorInstanceMarkerReadResult>(readCompletion.Task);
            },
        };
        var processProbe = new RecordingUnityGuiEditorProcessProbe();
        var service = new DaemonGuiEditorAttachService(
            markerReader,
            processProbe,
            new RecordingDaemonGuiSessionRegistrationAwaiter(),
            new RecordingDaemonGuiRebootstrapClient(),
            new RecordingDaemonDiagnosisStore(),
            new DaemonCompensationOperationOwner(),
            timeProvider);
        var timeout = TimeSpan.FromSeconds(1);

        var resultTask = service.TryAttachExistingGuiEditorAsync(
                DaemonGuiEditorAttachServiceTestSupport.UnityProject,
                timeout,
                editorMode: null,
                DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(timeout);
            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.NotNull(result);
            Assert.Equal(ExecutionErrorKind.Timeout, result!.Error!.Kind);
            Assert.Empty(processProbe.Invocations);
        }
        finally
        {
            readCompletion.TrySetResult(UnityEditorInstanceMarkerReadResult.Success(null));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryAttachExistingGuiEditor_WhenProcessProbeIgnoresCancellation_ReturnsAtDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probeCompletion = new TaskCompletionSource<UnityGuiEditorProcessProbeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var marker = DaemonGuiEditorAttachServiceTestSupport.CreateMarker();
        var processProbe = new RecordingUnityGuiEditorProcessProbe
        {
            ProbeAsyncHandler = (_, _) =>
            {
                probeStarted.TrySetResult();
                return new ValueTask<UnityGuiEditorProcessProbeResult>(probeCompletion.Task);
            },
        };
        var service = new DaemonGuiEditorAttachService(
            new RecordingUnityEditorInstanceMarkerReader
            {
                ReadResult = UnityEditorInstanceMarkerReadResult.Success(marker),
            },
            processProbe,
            new RecordingDaemonGuiSessionRegistrationAwaiter(),
            new RecordingDaemonGuiRebootstrapClient(),
            new RecordingDaemonDiagnosisStore(),
            new DaemonCompensationOperationOwner(),
            timeProvider);
        var timeout = TimeSpan.FromSeconds(1);

        var resultTask = service.TryAttachExistingGuiEditorAsync(
                DaemonGuiEditorAttachServiceTestSupport.UnityProject,
                timeout,
                editorMode: null,
                DaemonStartupBlockedProcessPolicy.Auto,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await probeStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(timeout);
            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.NotNull(result);
            Assert.Equal(ExecutionErrorKind.Timeout, result!.Error!.Kind);
        }
        finally
        {
            probeCompletion.TrySetResult(UnityGuiEditorProcessProbeResult.NotMatching(
                UnityGuiEditorProcessProbeStatus.NotRunning));
        }
    }

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
            new DaemonCompensationOperationOwner(),
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
        awaiter.Results.Enqueue(DaemonGuiSessionRegistrationWaitResult.Success(DaemonGuiEditorAttachServiceTestSupport.CreateGuiSession()));
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
            new DaemonCompensationOperationOwner(),
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

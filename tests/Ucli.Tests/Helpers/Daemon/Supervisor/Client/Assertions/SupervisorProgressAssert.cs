using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class SupervisorProgressAssert
{
    public static void EnsureRunningCompletedSuccessfully (
        CollectingCommandProgressSink progressSink,
        int expectedTimeoutMilliseconds)
    {
        var completedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[^1].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(CommandProgressResult.Succeeded), completedEntry.Result);
        Assert.Equal("started", completedEntry.StartStatus);
        Assert.Equal("running", completedEntry.DaemonStatus);
        Assert.Equal(expectedTimeoutMilliseconds, completedEntry.TimeoutMilliseconds);
    }

    public static void EnsureRunningCompletedWithFailure (
        CollectingCommandProgressSink progressSink,
        string expectedErrorCode)
    {
        var completedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[^1].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(CommandProgressResult.Failed), completedEntry.Result);
        Assert.Equal("failed", completedEntry.StartStatus);
        Assert.Equal("notRunning", completedEntry.DaemonStatus);
        Assert.Equal(expectedErrorCode, completedEntry.ErrorCode);
    }

    public static void WaitingForEndpointProgressForwarded (
        CollectingCommandProgressSink progressSink,
        string expectedProjectFingerprint,
        string? expectedMessage = null)
    {
        var progress = Assert.Single(progressSink.Entries);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint), progress.EventName);
        var payload = Assert.IsType<DaemonStartStartupObservationProgressEntry>(progress.Payload);
        Assert.Equal("startupObservation", payload.PayloadKind);
        Assert.Equal(expectedProjectFingerprint, payload.ProjectFingerprint);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupStatus.WaitingForEndpoint), payload.StartupStatus);
        if (expectedMessage != null)
        {
            Assert.Equal(expectedMessage, payload.Message);
        }
    }

    public static void LifecycleSnapshotProgressForwarded (CollectingCommandProgressSink progressSink)
    {
        var progress = Assert.Single(progressSink.Entries);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartProgressEvent.LifecycleObserved), progress.EventName);
        var payload = Assert.IsType<DaemonStartLifecycleSnapshotProgressEntry>(progress.Payload);
        Assert.Equal("lifecycleSnapshot", payload.PayloadKind);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcEditorLifecycleState.Compiling), payload.LifecycleState);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcEditorBlockingReason.Compile), payload.BlockingReason);
        Assert.False(payload.CanAcceptExecutionRequests);
    }
}

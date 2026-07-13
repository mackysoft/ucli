namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class SupervisorProjectGatewayAssert
{
    public static void BootstrapFailureProgressEmitted (
        DaemonStartResult result,
        RecordingSupervisorProcessLauncher launcher,
        CollectingCommandProgressSink progressSink,
        string expectedStorageRoot,
        UcliCode expectedErrorCode)
    {
        Assert.False(result.IsSuccess);
        var invocation = Assert.Single(launcher.Invocations);
        Assert.Equal(expectedStorageRoot, invocation.StorageRoot);
        Assert.Equal(expectedErrorCode, result.Error!.Code);

        Assert.Collection(
            progressSink.Entries,
            static entry => Assert.Equal(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapStarted),
                entry.EventName),
            static entry => Assert.Equal(
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.SupervisorBootstrapCompleted),
                entry.EventName));
        var completedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[^1].Payload);
        Assert.Equal(CommandProgressResult.Failed, completedEntry.Result);
        Assert.Null(completedEntry.StartStatus);
        Assert.Null(completedEntry.DaemonStatus);
        Assert.Equal(expectedErrorCode.Value, completedEntry.ErrorCode);
    }
}

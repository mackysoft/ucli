namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonStartProgressAssert
{
    public static void BatchmodeStartCompletedSuccessfully (
        CollectingCommandProgressSink progressSink,
        string expectedProjectFingerprint,
        int expectedTimeoutMilliseconds)
    {
        var startedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[0].Payload);
        Assert.Equal(expectedProjectFingerprint, startedEntry.ProjectFingerprint);
        Assert.Equal(expectedTimeoutMilliseconds, startedEntry.TimeoutMilliseconds);
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
            entry => Assert.Equal(expectedTimeoutMilliseconds, entry.TimeoutMilliseconds));
    }

    public static void CompletedWithStartupFailure (
        CollectingCommandProgressSink progressSink,
        string expectedErrorCode)
    {
        var completedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[^1].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(CommandProgressResult.Failed), completedEntry.Result);
        Assert.Equal("failed", completedEntry.StartStatus);
        Assert.Equal("notRunning", completedEntry.DaemonStatus);
        Assert.Equal(expectedErrorCode, completedEntry.ErrorCode);
    }

    public static void PluginVerificationFailurePayloads (
        CollectingCommandProgressSink progressSink,
        string expectedErrorCode)
    {
        var pluginCompletedEntry = Assert.IsType<DaemonStartProgressEntry>(progressSink.Entries[2].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(CommandProgressResult.Failed), pluginCompletedEntry.Result);
        Assert.Equal(expectedErrorCode, pluginCompletedEntry.ErrorCode);
        CompletedWithStartupFailure(progressSink, expectedErrorCode);
    }
}

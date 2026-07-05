namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class UnityDaemonReadinessGateAssert
{
    public static void RejectedWithoutDispatch (
        UnityRequestExecutionResult result,
        RecordingUnityIpcClient daemonClient,
        UcliCode expectedErrorCode,
        string? expectedMessageFragment = null)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        if (expectedMessageFragment is not null)
        {
            Assert.Contains(expectedMessageFragment, result.Message, StringComparison.Ordinal);
        }

        Assert.Empty(daemonClient.Invocations);
        Assert.Empty(daemonClient.StreamingInvocations);
    }

    public static void TimedOutBeforeProbeAndDispatch (
        UnityRequestExecutionResult result,
        RecordingDaemonPingInfoClient pingClient,
        RecordingUnityIpcClient daemonClient)
    {
        RejectedWithoutDispatch(
            result,
            daemonClient,
            ExecutionErrorCodes.IpcTimeout);
        Assert.Empty(pingClient.Invocations);
    }
}

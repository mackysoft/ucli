namespace MackySoft.Ucli.Tests.Helpers.Process;

internal static class UnityBatchmodeProcessHandleAssert
{
    public static void WaitedForExitWithoutTermination (StubUnityBatchmodeProcessHandle processHandle)
    {
        Assert.Single(processHandle.WaitForExitInvocations);
        Assert.Empty(processHandle.TerminateInvocations);
    }

    public static void WasNotTerminated (StubUnityBatchmodeProcessHandle processHandle)
    {
        Assert.Empty(processHandle.TerminateInvocations);
    }

    public static StubUnityBatchmodeProcessHandle.TerminateInvocation TerminatedOnce (
        StubUnityBatchmodeProcessHandle processHandle)
    {
        return Assert.Single(processHandle.TerminateInvocations);
    }

    public static StubUnityBatchmodeProcessHandle.TerminateInvocation WaitedForExitAndTerminatedOnce (
        StubUnityBatchmodeProcessHandle processHandle)
    {
        Assert.NotEmpty(processHandle.WaitForExitInvocations);
        return TerminatedOnce(processHandle);
    }

    public static StubUnityBatchmodeProcessHandle.TerminateInvocation TerminatedOnceWithMode (
        StubUnityBatchmodeProcessHandle processHandle,
        ProcessTerminationMode expectedMode)
    {
        var invocation = Assert.Single(processHandle.TerminateInvocations);
        Assert.NotNull(invocation.TerminationPolicy);
        Assert.Equal(expectedMode, invocation.TerminationPolicy!.Mode);
        return invocation;
    }

    public static StubUnityBatchmodeProcessHandle.TerminateInvocation WaitedForExitAndTerminatedOnceWithMode (
        StubUnityBatchmodeProcessHandle processHandle,
        ProcessTerminationMode expectedMode)
    {
        Assert.NotEmpty(processHandle.WaitForExitInvocations);
        return TerminatedOnceWithMode(processHandle, expectedMode);
    }
}

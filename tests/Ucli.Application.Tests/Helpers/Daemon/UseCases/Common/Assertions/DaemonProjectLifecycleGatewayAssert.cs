using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonProjectLifecycleGatewayAssert
{
    public static RecordingDaemonProjectLifecycleGateway.EnsureRunningInvocation EnsureRunningRequested (
        RecordingDaemonProjectLifecycleGateway gateway,
        ResolvedUnityProjectContext expectedUnityProject,
        TimeSpan maximumTimeout,
        DaemonEditorMode? expectedEditorMode = null,
        DaemonStartupBlockedProcessPolicy? expectedStartupBlockedPolicy = null)
    {
        var invocation = Assert.Single(gateway.EnsureRunningInvocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.True(invocation.Timeout > TimeSpan.Zero);
        Assert.True(invocation.Timeout <= maximumTimeout);
        if (expectedEditorMode.HasValue)
        {
            Assert.Equal(expectedEditorMode.Value, invocation.EditorMode);
        }

        if (expectedStartupBlockedPolicy.HasValue)
        {
            Assert.Equal(expectedStartupBlockedPolicy.Value, invocation.OnStartupBlocked);
        }

        return invocation;
    }

    public static RecordingDaemonProjectLifecycleGateway.EnsureRunningInvocation EnsureRunningRequestedWithExactTimeout (
        RecordingDaemonProjectLifecycleGateway gateway,
        ResolvedUnityProjectContext expectedUnityProject,
        TimeSpan expectedTimeout)
    {
        var invocation = Assert.Single(gateway.EnsureRunningInvocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        return invocation;
    }

    public static RecordingDaemonProjectLifecycleGateway.EnsureRunningInvocation EnsureRunningRequestedWithProgressSink (
        RecordingDaemonProjectLifecycleGateway gateway,
        ICommandProgressSink expectedSupervisorProgressSink)
    {
        var invocation = Assert.Single(gateway.EnsureRunningInvocations);
        Assert.Same(expectedSupervisorProgressSink, invocation.SupervisorProgressSink);
        return invocation;
    }

    public static RecordingDaemonProjectLifecycleGateway.TryStopProjectInvocation TryStopProjectRequested (
        RecordingDaemonProjectLifecycleGateway gateway,
        ResolvedUnityProjectContext expectedUnityProject,
        TimeSpan maximumTimeout)
    {
        var invocation = Assert.Single(gateway.TryStopProjectInvocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.True(invocation.Timeout > TimeSpan.Zero);
        Assert.True(invocation.Timeout <= maximumTimeout);
        return invocation;
    }
}

using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Tests;

internal static class DaemonStartServiceAssert
{
    public static RecordingDaemonStartService.Invocation StartRequestedWithOptions (
        RecordingDaemonStartService service,
        string? expectedProjectPath,
        int? expectedTimeoutMilliseconds,
        UnityEditorMode? expectedEditorMode,
        DaemonStartupBlockedProcessPolicy expectedOnStartupBlocked)
    {
        var invocation = Assert.Single(service.Invocations);
        ProjectPathDispatchAssert.EqualNormalized(expectedProjectPath, invocation.ProjectPath);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.TimeoutMilliseconds);
        Assert.Equal(expectedEditorMode, invocation.EditorMode);
        Assert.Equal(expectedOnStartupBlocked, invocation.OnStartupBlocked);
        return invocation;
    }
}

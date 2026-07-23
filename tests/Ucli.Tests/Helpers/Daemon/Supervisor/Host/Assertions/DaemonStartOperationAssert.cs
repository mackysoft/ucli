using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonStartOperationAssert
{
    public static void EnsureRunningRequestRejectedBeforeStartOperation (
        IpcResponse response,
        RecordingDaemonStartOperation startOperation,
        UcliCode expectedErrorCode,
        string? expectedMessageFragment = null)
    {
        Assert.Equal(IpcResponseStatus.Error, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(expectedErrorCode, error.Code);
        if (expectedMessageFragment is not null)
        {
            Assert.Contains(expectedMessageFragment, error.Message, StringComparison.Ordinal);
        }

        Assert.Empty(startOperation.Invocations);
    }

    public static RecordingDaemonStartOperation.Invocation EnsureRunningRequested (
        RecordingDaemonStartOperation startOperation,
        AbsolutePath expectedRepositoryRoot,
        AbsolutePath expectedUnityProjectRoot,
        ProjectFingerprint expectedProjectFingerprint,
        TimeSpan maximumTimeout,
        DaemonEditorMode? expectedEditorMode,
        DaemonStartupBlockedProcessPolicy expectedStartupBlockedPolicy)
    {
        var invocation = Assert.Single(startOperation.Invocations);
        FileSystemAssert.ForPath(invocation.UnityProject.RepositoryRoot.Value)
            .EqualsNormalized(expectedRepositoryRoot.Value);
        FileSystemAssert.ForPath(invocation.UnityProject.UnityProjectRoot.Value)
            .EqualsNormalized(expectedUnityProjectRoot.Value);
        Assert.Equal(expectedProjectFingerprint, invocation.UnityProject.ProjectFingerprint);
        Assert.True(invocation.RemainingTimeout > TimeSpan.Zero);
        Assert.True(invocation.RemainingTimeout <= maximumTimeout);
        Assert.Equal(expectedEditorMode, invocation.EditorMode);
        Assert.Equal(expectedStartupBlockedPolicy, invocation.OnStartupBlocked);
        return invocation;
    }

    public static RecordingDaemonStartOperation.Invocation EnsureRunningStreamRequested (
        RecordingDaemonStartOperation startOperation,
        AbsolutePath expectedRepositoryRoot,
        AbsolutePath expectedUnityProjectRoot,
        ProjectFingerprint expectedProjectFingerprint,
        TimeSpan maximumTimeout,
        DaemonEditorMode? expectedEditorMode,
        DaemonStartupBlockedProcessPolicy expectedStartupBlockedPolicy)
    {
        var invocation = EnsureRunningRequested(
            startOperation,
            expectedRepositoryRoot,
            expectedUnityProjectRoot,
            expectedProjectFingerprint,
            maximumTimeout,
            expectedEditorMode,
            expectedStartupBlockedPolicy);
        Assert.NotNull(invocation.ProgressObserver);
        return invocation;
    }
}

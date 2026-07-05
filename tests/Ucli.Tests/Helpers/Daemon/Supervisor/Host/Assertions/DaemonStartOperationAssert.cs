using MackySoft.Tests;
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
        Assert.Equal(IpcProtocol.StatusError, response.Status);
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
        string expectedRepositoryRoot,
        string expectedUnityProjectRoot,
        string expectedProjectFingerprint,
        TimeSpan maximumTimeout,
        DaemonEditorMode? expectedEditorMode,
        DaemonStartupBlockedProcessPolicy expectedStartupBlockedPolicy)
    {
        var invocation = Assert.Single(startOperation.Invocations);
        FileSystemAssert.ForPath(invocation.UnityProject.RepositoryRoot)
            .EqualsNormalized(expectedRepositoryRoot);
        FileSystemAssert.ForPath(invocation.UnityProject.UnityProjectRoot)
            .EqualsNormalized(expectedUnityProjectRoot);
        Assert.Equal(expectedProjectFingerprint, invocation.UnityProject.ProjectFingerprint);
        Assert.True(invocation.Timeout > TimeSpan.Zero);
        Assert.True(invocation.Timeout <= maximumTimeout);
        Assert.Equal(expectedEditorMode, invocation.EditorMode);
        Assert.Equal(expectedStartupBlockedPolicy, invocation.OnStartupBlocked);
        return invocation;
    }

    public static RecordingDaemonStartOperation.Invocation EnsureRunningStreamRequested (
        RecordingDaemonStartOperation startOperation,
        string expectedRepositoryRoot,
        string expectedUnityProjectRoot,
        string expectedProjectFingerprint,
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

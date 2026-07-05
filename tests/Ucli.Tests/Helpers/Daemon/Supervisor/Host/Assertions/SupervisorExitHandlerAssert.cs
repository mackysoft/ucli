namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class SupervisorExitHandlerAssert
{
    public static void SessionReadFailureLoggedAfterCleanup (
        RecordingDaemonArtifactCleaner artifactCleaner,
        ResolvedUnityProjectContext expectedUnityProject,
        string logText,
        string expectedReadFailureMessage)
    {
        var invocation = Assert.Single(artifactCleaner.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Contains("Supervisor session read failed during exit cleanup.", logText, StringComparison.Ordinal);
        Assert.Contains(expectedReadFailureMessage, logText, StringComparison.Ordinal);
    }
}

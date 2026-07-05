namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal static class UnityPluginLocatorAssert
{
    public static RecordingUnityUcliPluginLocator.Invocation VerificationAttemptedFor (
        RecordingUnityUcliPluginLocator pluginLocator,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        var invocation = Assert.Single(pluginLocator.Invocations);
        Assert.Equal(expectedUnityProject.UnityProjectRoot, invocation.UnityProjectRoot);
        return invocation;
    }
}

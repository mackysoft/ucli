using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Tests;

internal static class UnityPluginVerifierAssert
{
    public static RecordingUnityPluginVerifier.Invocation VerificationRequestedFor (
        RecordingUnityPluginVerifier pluginVerifier,
        AbsolutePath expectedUnityProjectRoot)
    {
        var invocation = Assert.Single(pluginVerifier.Invocations);
        Assert.Equal(expectedUnityProjectRoot, invocation.UnityProjectRoot);
        return invocation;
    }
}

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal static class UnityVersionResolverAssert
{
    public static RecordingUnityVersionResolver.Invocation ResolvedOnceFor (
        RecordingUnityVersionResolver unityVersionResolver,
        string expectedUnityProjectRoot,
        string? expectedPreferredUnityVersion = null)
    {
        var invocation = Assert.Single(unityVersionResolver.Invocations);
        Assert.Equal(expectedUnityProjectRoot, invocation.UnityProjectRoot);
        Assert.Equal(expectedPreferredUnityVersion, invocation.PreferredUnityVersion);
        return invocation;
    }
}

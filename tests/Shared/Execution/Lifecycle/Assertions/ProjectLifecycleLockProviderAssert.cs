namespace MackySoft.Ucli.TestSupport;

internal static class ProjectLifecycleLockProviderAssert
{
    public static StubProjectLifecycleLockProvider.Invocation LifecycleLockAcquiredFor (
        StubProjectLifecycleLockProvider lockProvider,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        return AcquiredOnceFor(lockProvider, expectedUnityProject.UnityProjectRoot);
    }

    public static StubProjectLifecycleLockProvider.Invocation AcquiredOnceFor (
        StubProjectLifecycleLockProvider lockProvider,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        return AcquiredOnceFor(lockProvider, expectedUnityProject.UnityProjectRoot);
    }

    public static StubProjectLifecycleLockProvider.Invocation AcquiredOnceFor (
        StubProjectLifecycleLockProvider lockProvider,
        string expectedUnityProjectRoot)
    {
        var invocation = Assert.Single(lockProvider.Invocations);
        Assert.Equal(expectedUnityProjectRoot, invocation.Request.UnityProjectRoot);
        return invocation;
    }
}

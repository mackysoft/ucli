using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonLifecycleObservationAssert
{
    public static void LifecycleObservationReadTwiceFor (
        RecordingDaemonLifecycleStore lifecycleStore,
        ProjectContext expectedProjectContext,
        CancellationToken expectedCancellationToken)
    {
        Assert.Collection(
            lifecycleStore.ReadInvocations,
            invocation => AssertLifecycleObservationRead(invocation, expectedProjectContext, expectedCancellationToken),
            invocation => AssertLifecycleObservationRead(invocation, expectedProjectContext, expectedCancellationToken));
    }

    private static void AssertLifecycleObservationRead (
        RecordingDaemonLifecycleStore.ReadInvocation invocation,
        ProjectContext expectedProjectContext,
        CancellationToken expectedCancellationToken)
    {
        Assert.Equal(expectedProjectContext.UnityProject.RepositoryRoot, invocation.StorageRoot);
        Assert.Equal(expectedProjectContext.UnityProject.ProjectFingerprint, invocation.ProjectFingerprint);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
    }
}

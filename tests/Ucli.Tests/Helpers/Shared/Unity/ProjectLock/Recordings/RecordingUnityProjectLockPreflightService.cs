using MackySoft.Ucli.Shared.Unity.ProjectLock;

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal sealed class RecordingUnityProjectLockPreflightService : IUnityProjectLockPreflightService
{
    private readonly IReadOnlyList<UnityProjectLockPreflightResult> prepareResults;
    private readonly List<Invocation> cleanupInvocations = [];
    private readonly List<Invocation> prepareInvocations = [];

    private int nextPrepareResultIndex;

    public RecordingUnityProjectLockPreflightService (params UnityProjectLockPreflightResult[] prepareResults)
    {
        this.prepareResults = prepareResults is { Length: > 0 }
            ? prepareResults
            : [UnityProjectLockPreflightResult.Unlocked("/tmp/unity-project/Temp/UnityLockfile")];
    }

    public UnityProjectLockPreflightResult CleanupResult { get; set; }
        = UnityProjectLockPreflightResult.Unlocked("/tmp/unity-project/Temp/UnityLockfile");

    public IReadOnlyList<Invocation> PrepareInvocations => prepareInvocations;

    public IReadOnlyList<Invocation> CleanupInvocations => cleanupInvocations;

    public void AssertStartPreflightRetriedFor (
        ResolvedUnityProjectContext expectedUnityProject,
        CancellationToken expectedCancellationToken)
    {
        Assert.Collection(
            prepareInvocations,
            invocation => AssertStartPreflightInvocation(invocation, expectedUnityProject, expectedCancellationToken),
            invocation => AssertStartPreflightInvocation(invocation, expectedUnityProject, expectedCancellationToken));
    }

    public void AssertOnlyInitialStartPreflightFor (
        ResolvedUnityProjectContext expectedUnityProject,
        CancellationToken expectedCancellationToken)
    {
        var invocation = Assert.Single(prepareInvocations);
        AssertStartPreflightInvocation(invocation, expectedUnityProject, expectedCancellationToken);
    }

    public ValueTask<UnityProjectLockPreflightResult> PrepareForUnityProcessStartAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        prepareInvocations.Add(new Invocation(unityProject, cancellationToken));

        var resultIndex = Math.Min(nextPrepareResultIndex, prepareResults.Count - 1);
        nextPrepareResultIndex++;
        return ValueTask.FromResult(prepareResults[resultIndex]);
    }

    public ValueTask<UnityProjectLockPreflightResult> CleanupStaleLockAfterUnityProcessExitAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        cleanupInvocations.Add(new Invocation(unityProject, cancellationToken));

        return ValueTask.FromResult(CleanupResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        CancellationToken CancellationToken);

    private static void AssertStartPreflightInvocation (
        Invocation invocation,
        ResolvedUnityProjectContext expectedUnityProject,
        CancellationToken expectedCancellationToken)
    {
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
    }
}

using MackySoft.FileSystem;
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
            : [UnityProjectLockPreflightResult.Unlocked(DefaultLockFilePath)];
    }

    public UnityProjectLockPreflightResult CleanupResult { get; set; }
        = UnityProjectLockPreflightResult.Unlocked(DefaultLockFilePath);

    public Func<ResolvedUnityProjectContext, CancellationToken, ValueTask<UnityProjectLockPreflightResult>>? PrepareAsyncHandler { get; set; }

    public Func<ResolvedUnityProjectContext, CancellationToken, ValueTask<UnityProjectLockPreflightResult>>? CleanupAsyncHandler { get; set; }

    public IReadOnlyList<Invocation> PrepareInvocations => prepareInvocations;

    public IReadOnlyList<Invocation> CleanupInvocations => cleanupInvocations;

    private static AbsolutePath DefaultLockFilePath { get; } = AbsolutePath.Resolve(
        AbsolutePath.Parse(Environment.CurrentDirectory),
        "unity-project/Temp/UnityLockfile");

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

        if (PrepareAsyncHandler is not null)
        {
            return PrepareAsyncHandler(unityProject, cancellationToken);
        }

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

        if (CleanupAsyncHandler is not null)
        {
            return CleanupAsyncHandler(unityProject, cancellationToken);
        }

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

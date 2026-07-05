using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonArtifactCleaner : IDaemonArtifactCleaner
{
    private readonly List<Invocation> invocations = [];

    public DaemonArtifactCleanupResult NextResult { get; set; } =
        DaemonArtifactCleanupResult.Success();

    public Func<ResolvedUnityProjectContext, CancellationToken, Task<DaemonArtifactCleanupResult>>? CleanupHandler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonArtifactCleanupResult> CleanupAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(unityProject, cancellationToken));
        if (CleanupHandler is not null)
        {
            return new ValueTask<DaemonArtifactCleanupResult>(CleanupHandler(unityProject, cancellationToken));
        }

        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        CancellationToken CancellationToken);
}

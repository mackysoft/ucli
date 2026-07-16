using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonArtifactCleaner : IDaemonArtifactCleaner
{
    private readonly List<Invocation> invocations = [];

    public DaemonArtifactCleanupResult NextResult { get; set; } =
        DaemonArtifactCleanupResult.Success();

    public Func<ResolvedUnityProjectContext, ExecutionDeadline, CancellationToken, Task<DaemonArtifactCleanupResult>>? CleanupHandler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionMissingAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(
            unityProject,
            ExpectedSession: null,
            ExpectedArtifactIdentity: null,
            ExpectedStoppedProcess: null,
            deadline,
            cancellationToken));
        if (CleanupHandler is not null)
        {
            return new ValueTask<DaemonArtifactCleanupResult>(CleanupHandler(unityProject, deadline, cancellationToken));
        }

        return ValueTask.FromResult(NextResult);
    }

    public ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession expectedSession,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(expectedSession);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(
            unityProject,
            expectedSession,
            ExpectedArtifactIdentity: null,
            ExpectedStoppedProcess: null,
            deadline,
            cancellationToken));
        if (CleanupHandler is not null)
        {
            return new ValueTask<DaemonArtifactCleanupResult>(CleanupHandler(unityProject, deadline, cancellationToken));
        }

        return ValueTask.FromResult(NextResult);
    }

    public ValueTask<DaemonArtifactCleanupResult> CleanupIfStoppedProcessMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonProcessTerminationTarget stoppedProcess,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(
            unityProject,
            ExpectedSession: null,
            ExpectedArtifactIdentity: null,
            stoppedProcess,
            deadline,
            cancellationToken));
        if (CleanupHandler is not null)
        {
            return new ValueTask<DaemonArtifactCleanupResult>(CleanupHandler(unityProject, deadline, cancellationToken));
        }

        return ValueTask.FromResult(NextResult);
    }

    public ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionArtifactMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionArtifactIdentity expectedArtifactIdentity,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(expectedArtifactIdentity);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(
            unityProject,
            ExpectedSession: null,
            expectedArtifactIdentity,
            ExpectedStoppedProcess: null,
            deadline,
            cancellationToken));
        if (CleanupHandler is not null)
        {
            return new ValueTask<DaemonArtifactCleanupResult>(CleanupHandler(unityProject, deadline, cancellationToken));
        }

        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSession? ExpectedSession,
        DaemonSessionArtifactIdentity? ExpectedArtifactIdentity,
        DaemonProcessTerminationTarget? ExpectedStoppedProcess,
        ExecutionDeadline Deadline,
        CancellationToken CancellationToken);
}

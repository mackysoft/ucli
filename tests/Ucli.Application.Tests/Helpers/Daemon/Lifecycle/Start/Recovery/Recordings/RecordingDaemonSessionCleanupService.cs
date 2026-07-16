using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingDaemonSessionCleanupService : IDaemonSessionCleanupService
{
    private readonly List<InvalidSessionInvocation> invalidSessionInvocations = [];
    private readonly List<StaleSessionInvocation> staleSessionInvocations = [];

    public DaemonSessionStoreOperationResult CleanupInvalidSessionArtifactsResult { get; set; } =
        DaemonSessionStoreOperationResult.Success();

    public DaemonSessionStoreOperationResult CleanupStaleSessionArtifactsResult { get; set; } =
        DaemonSessionStoreOperationResult.Success();

    public IReadOnlyList<InvalidSessionInvocation> InvalidSessionInvocations => invalidSessionInvocations;

    public IReadOnlyList<StaleSessionInvocation> StaleSessionInvocations => staleSessionInvocations;

    public ValueTask<DaemonSessionStoreOperationResult> CleanupInvalidSessionArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.TryGetRemainingTimeout(out var remainingTimeout);

        invalidSessionInvocations.Add(new InvalidSessionInvocation(
            unityProject,
            readResult,
            deadline,
            remainingTimeout,
            cancellationToken));

        return ValueTask.FromResult(CleanupInvalidSessionArtifactsResult);
    }

    public ValueTask<DaemonSessionStoreOperationResult> CleanupStaleSessionArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(deadline);
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.TryGetRemainingTimeout(out var remainingTimeout);

        staleSessionInvocations.Add(new StaleSessionInvocation(
            unityProject,
            session,
            deadline,
            remainingTimeout,
            cancellationToken));

        return ValueTask.FromResult(CleanupStaleSessionArtifactsResult);
    }

    internal readonly record struct InvalidSessionInvocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSessionReadResult ReadResult,
        ExecutionDeadline Deadline,
        TimeSpan RemainingTimeout,
        CancellationToken CancellationToken);

    internal readonly record struct StaleSessionInvocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSession Session,
        ExecutionDeadline Deadline,
        TimeSpan RemainingTimeout,
        CancellationToken CancellationToken);
}

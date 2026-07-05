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
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        invalidSessionInvocations.Add(new InvalidSessionInvocation(unityProject, readResult, timeout, cancellationToken));

        return ValueTask.FromResult(CleanupInvalidSessionArtifactsResult);
    }

    public ValueTask<DaemonSessionStoreOperationResult> CleanupStaleSessionArtifactsAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        staleSessionInvocations.Add(new StaleSessionInvocation(unityProject, session, timeout, cancellationToken));

        return ValueTask.FromResult(CleanupStaleSessionArtifactsResult);
    }

    internal readonly record struct InvalidSessionInvocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSessionReadResult ReadResult,
        TimeSpan Timeout,
        CancellationToken CancellationToken);

    internal readonly record struct StaleSessionInvocation (
        ResolvedUnityProjectContext UnityProject,
        DaemonSession Session,
        TimeSpan Timeout,
        CancellationToken CancellationToken);
}

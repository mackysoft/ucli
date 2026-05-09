using MackySoft.Ucli.Application.Shared.Context.Project;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Implements lock-file preflight for Unity process startup and post-exit cleanup. </summary>
internal sealed class UnityProjectLockPreflightService : IUnityProjectLockPreflightService
{
    private readonly IUnityProjectLockFileProbe lockFileProbe;

    private readonly IUnityProjectLockOwnerProbe ownerProbe;

    private readonly IUnityProjectLockFileCleaner lockFileCleaner;

    /// <summary> Initializes a new instance of the <see cref="UnityProjectLockPreflightService" /> class. </summary>
    /// <param name="lockFileProbe"> The lock-file probe dependency. </param>
    /// <param name="ownerProbe"> The lock owner probe dependency. </param>
    /// <param name="lockFileCleaner"> The lock-file cleaner dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public UnityProjectLockPreflightService (
        IUnityProjectLockFileProbe lockFileProbe,
        IUnityProjectLockOwnerProbe ownerProbe,
        IUnityProjectLockFileCleaner lockFileCleaner)
    {
        this.lockFileProbe = lockFileProbe ?? throw new ArgumentNullException(nameof(lockFileProbe));
        this.ownerProbe = ownerProbe ?? throw new ArgumentNullException(nameof(ownerProbe));
        this.lockFileCleaner = lockFileCleaner ?? throw new ArgumentNullException(nameof(lockFileCleaner));
    }

    /// <inheritdoc />
    public ValueTask<UnityProjectLockPreflightResult> PrepareForUnityProcessStartAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return InspectAndClearStaleLockAsync(unityProject, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<UnityProjectLockPreflightResult> CleanupStaleLockAfterUnityProcessExitAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        return InspectAndClearStaleLockAsync(unityProject, cancellationToken);
    }

    private async ValueTask<UnityProjectLockPreflightResult> InspectAndClearStaleLockAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProject.UnityProjectRoot);

        var lockFileResult = lockFileProbe.Probe(unityProject.UnityProjectRoot);
        if (!lockFileResult.IsSuccess)
        {
            return UnityProjectLockPreflightResult.InspectionFailed(lockFileResult.ErrorMessage!);
        }

        if (!lockFileResult.IsLocked)
        {
            return UnityProjectLockPreflightResult.Unlocked(lockFileResult.LockFilePath!);
        }

        var lockFilePath = lockFileResult.LockFilePath!;
        var ownerResult = await ownerProbe.ProbeOwnerAsync(unityProject, lockFilePath, cancellationToken).ConfigureAwait(false);
        switch (ownerResult.Status)
        {
            case UnityProjectLockOwnerProbeStatus.ActiveOwner:
                return UnityProjectLockPreflightResult.ActiveLock(
                    lockFilePath,
                    ownerResult.Message ?? UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProject.UnityProjectRoot, lockFilePath));

            case UnityProjectLockOwnerProbeStatus.Ambiguous:
                return UnityProjectLockPreflightResult.Ambiguous(
                    lockFilePath,
                    ownerResult.Message ?? UnityProjectLockFailureMessage.CreateAmbiguous(unityProject.UnityProjectRoot, lockFilePath, "Lock owner could not be determined safely."));

            case UnityProjectLockOwnerProbeStatus.NoOwner:
                var cleanupResult = lockFileCleaner.Delete(lockFilePath);
                if (cleanupResult.IsSuccess)
                {
                    return UnityProjectLockPreflightResult.StaleLockCleared(lockFilePath);
                }

                return UnityProjectLockPreflightResult.CleanupFailed(
                    lockFilePath,
                    cleanupResult.ErrorMessage ?? UnityProjectLockFailureMessage.CreateCleanupFailed(lockFilePath, "Unknown cleanup failure."));

            default:
                throw new ArgumentOutOfRangeException(nameof(ownerResult), ownerResult.Status, "Unknown Unity project lock owner probe status.");
        }
    }
}

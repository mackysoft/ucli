using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Infrastructure.Execution;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Determines whether Unity's project lock file is owned by a live process. </summary>
internal sealed class UnityProjectLockOwnerProbe : IUnityProjectLockOwnerProbe
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IUnityEditorInstanceProbe editorInstanceProbe;

    private readonly IUnityProjectProcessScanner projectProcessScanner;

    /// <summary> Initializes a new instance of the <see cref="UnityProjectLockOwnerProbe" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <param name="editorInstanceProbe"> The EditorInstance marker probe dependency. </param>
    /// <param name="projectProcessScanner"> The operating-system Unity process scanner dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public UnityProjectLockOwnerProbe (
        IDaemonSessionStore daemonSessionStore,
        IUnityEditorInstanceProbe editorInstanceProbe,
        IUnityProjectProcessScanner projectProcessScanner)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.editorInstanceProbe = editorInstanceProbe ?? throw new ArgumentNullException(nameof(editorInstanceProbe));
        this.projectProcessScanner = projectProcessScanner ?? throw new ArgumentNullException(nameof(projectProcessScanner));
    }

    /// <inheritdoc />
    public async ValueTask<UnityProjectLockOwnerProbeResult> ProbeOwnerAsync (
        ResolvedUnityProjectContext unityProject,
        string lockFilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);

        var sessionOwnerResult = await ProbeDaemonSessionOwnerAsync(unityProject, lockFilePath, cancellationToken).ConfigureAwait(false);
        if (sessionOwnerResult.Status != UnityProjectLockOwnerProbeStatus.NoOwner)
        {
            return sessionOwnerResult;
        }

        var editorInstanceResult = await editorInstanceProbe.ProbeAsync(unityProject.UnityProjectRoot, cancellationToken).ConfigureAwait(false);
        switch (editorInstanceResult.Status)
        {
            case UnityEditorInstanceProbeStatus.Active:
                return UnityProjectLockOwnerProbeResult.ActiveOwner(
                    UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProject.UnityProjectRoot, lockFilePath));

            case UnityEditorInstanceProbeStatus.Ambiguous:
                return UnityProjectLockOwnerProbeResult.Ambiguous(UnityProjectLockFailureMessage.CreateAmbiguous(
                    unityProject.UnityProjectRoot,
                    lockFilePath,
                    editorInstanceResult.Message ?? "EditorInstance marker could not be inspected safely."));

            case UnityEditorInstanceProbeStatus.NotFound:
            case UnityEditorInstanceProbeStatus.Stale:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(editorInstanceResult), editorInstanceResult.Status, "Unknown EditorInstance probe status.");
        }

        var processScanResult = await projectProcessScanner.FindProcessesForProjectAsync(
                unityProject.UnityProjectRoot,
                cancellationToken)
            .ConfigureAwait(false);
        if (!processScanResult.IsSuccess)
        {
            return UnityProjectLockOwnerProbeResult.Ambiguous(UnityProjectLockFailureMessage.CreateAmbiguous(
                unityProject.UnityProjectRoot,
                lockFilePath,
                processScanResult.ErrorMessage!));
        }

        if (processScanResult.Matches.Count > 0)
        {
            return UnityProjectLockOwnerProbeResult.ActiveOwner(
                UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProject.UnityProjectRoot, lockFilePath));
        }

        return UnityProjectLockOwnerProbeResult.NoOwner();
    }

    private async ValueTask<UnityProjectLockOwnerProbeResult> ProbeDaemonSessionOwnerAsync (
        ResolvedUnityProjectContext unityProject,
        string lockFilePath,
        CancellationToken cancellationToken)
    {
        var readResult = await daemonSessionStore.ReadAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            if (readResult.FailureKind == DaemonSessionReadFailureKind.InvalidSession)
            {
                return UnityProjectLockOwnerProbeResult.NoOwner();
            }

            return UnityProjectLockOwnerProbeResult.Ambiguous(UnityProjectLockFailureMessage.CreateAmbiguous(
                unityProject.UnityProjectRoot,
                lockFilePath,
                readResult.Error?.Message ?? "Daemon session could not be read safely."));
        }

        if (readResult.Session?.ProcessId is not int processId || processId <= 0)
        {
            return UnityProjectLockOwnerProbeResult.NoOwner();
        }

        if (!ProcessLivenessProbe.IsAlive(processId))
        {
            return UnityProjectLockOwnerProbeResult.NoOwner();
        }

        return UnityProjectLockOwnerProbeResult.ActiveOwner(
            UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProject.UnityProjectRoot, lockFilePath));
    }
}

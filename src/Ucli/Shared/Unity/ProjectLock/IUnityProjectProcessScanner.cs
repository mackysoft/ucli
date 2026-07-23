using MackySoft.FileSystem;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Scans operating-system processes for Unity processes targeting one project path. </summary>
internal interface IUnityProjectProcessScanner
{
    /// <summary> Finds live Unity processes whose <c>-projectPath</c> matches the target project. </summary>
    /// <param name="unityProjectRoot"> The resolved Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The scan result. </returns>
    ValueTask<UnityProjectProcessScanResult> FindProcessesForProjectAsync (
        AbsolutePath unityProjectRoot,
        CancellationToken cancellationToken = default);
}

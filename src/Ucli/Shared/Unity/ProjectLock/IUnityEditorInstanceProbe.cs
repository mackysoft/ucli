namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Probes Unity's project-local EditorInstance marker. </summary>
internal interface IUnityEditorInstanceProbe
{
    /// <summary> Reads and validates the EditorInstance marker for one Unity project. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The marker probe result. </returns>
    ValueTask<UnityEditorInstanceProbeResult> ProbeAsync (
        string unityProjectRoot,
        CancellationToken cancellationToken = default);
}

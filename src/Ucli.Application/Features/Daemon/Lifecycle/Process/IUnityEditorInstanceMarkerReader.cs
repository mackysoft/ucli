namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Reads Unity Editor instance markers from the target project root. </summary>
internal interface IUnityEditorInstanceMarkerReader
{
    /// <summary> Reads the marker from <c>Library/EditorInstance.json</c> under the resolved Unity project root. </summary>
    ValueTask<UnityEditorInstanceMarkerReadResult> Read (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}

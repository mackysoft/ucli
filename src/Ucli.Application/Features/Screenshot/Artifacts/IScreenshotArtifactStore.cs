namespace MackySoft.Ucli.Application.Features.Screenshot.Artifacts;

/// <summary> Creates capture-scoped ownership of screenshot staging and artifact storage. </summary>
internal interface IScreenshotArtifactStore
{
    /// <summary> Prepares one capture-scoped staging and artifact lease. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="captureId"> The collision-resistant capture identifier. </param>
    /// <returns> The prepared lease, or a structured preparation error. </returns>
    ScreenshotArtifactPreparationResult Prepare (
        ResolvedUnityProjectContext unityProject,
        string captureId);
}

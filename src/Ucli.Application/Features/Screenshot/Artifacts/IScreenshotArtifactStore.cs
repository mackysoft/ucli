namespace MackySoft.Ucli.Application.Features.Screenshot.Artifacts;

/// <summary> Owns screenshot raw-staging paths and commits validated PNG artifacts. </summary>
internal interface IScreenshotArtifactStore
{
    /// <summary> Prepares one capture-scoped staging and artifact layout. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="captureId"> The collision-resistant capture identifier. </param>
    /// <returns> The prepared paths, or a structured preparation error. </returns>
    ScreenshotArtifactPreparationResult Prepare (
        ResolvedUnityProjectContext unityProject,
        string captureId);

    /// <summary> Validates one raw staging image and atomically commits its PNG artifact. </summary>
    /// <param name="request"> The captured raw-image contract and prepared paths. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The committed artifact reference, or a structured commit error. </returns>
    ValueTask<ScreenshotArtifactCommitResult> CommitAsync (
        ScreenshotArtifactCommitRequest request,
        CancellationToken cancellationToken = default);

    /// <summary> Discards one prepared capture layout without deleting a committed PNG artifact. </summary>
    /// <param name="paths"> The prepared capture paths. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The discard result. </returns>
    ValueTask<ScreenshotArtifactDiscardResult> DiscardAsync (
        ScreenshotArtifactPaths paths,
        CancellationToken cancellationToken = default);
}

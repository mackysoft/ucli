namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Prepares and writes build-run artifacts under local uCLI storage. </summary>
internal interface IBuildRunArtifactStore
{
    /// <summary> Prepares the build-run artifact directory and player output directory. </summary>
    BuildRunArtifactPreparationResult Prepare (
        ResolvedUnityProjectContext unityProject,
        string runId);

    /// <summary> Writes completed build-run artifacts and returns their references. </summary>
    ValueTask<BuildRunArtifactWriteOperationResult> WriteArtifactsAsync (
        BuildRunArtifactWriteRequest request,
        CancellationToken cancellationToken = default);
}

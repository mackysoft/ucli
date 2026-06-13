namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Prepares, accounts, and writes build-run artifacts under local uCLI storage. </summary>
internal interface IBuildRunArtifactStore
{
    /// <summary> Prepares the build-run artifact directory and player output directory. </summary>
    BuildRunArtifactPreparationResult Prepare (
        ResolvedUnityProjectContext unityProject,
        string runId);

    /// <summary> Accounts Unity-generated artifacts and writes the output manifest. </summary>
    ValueTask<BuildRunArtifactAccountingOperationResult> AccountArtifactsAsync (
        BuildRunArtifactAccountingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary> Writes <c>build.json</c> and returns its artifact reference. </summary>
    ValueTask<BuildArtifactRefWriteResult> WriteMetadataAsync (
        BuildRunMetadataWriteRequest request,
        CancellationToken cancellationToken = default);
}

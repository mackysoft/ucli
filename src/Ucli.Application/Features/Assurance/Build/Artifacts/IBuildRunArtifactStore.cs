using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Prepares, accounts, and writes build-run artifacts under local uCLI storage. </summary>
internal interface IBuildRunArtifactStore
{
    /// <summary> Prepares the build-run artifact directory and runner working output root. </summary>
    BuildRunArtifactPreparationResult Prepare (
        ResolvedUnityProjectContext unityProject,
        Guid runId);

    /// <summary> Prepares the command-derived BuildPipeline output layout before runner invocation. </summary>
    BuildRunArtifactPreparationResult PrepareBuildPipelineOutputLayout (
        BuildRunArtifactPaths paths,
        BuildPipelineOutputLayout outputLayout);

    /// <summary> Accounts Unity-generated artifacts and writes the output manifest. </summary>
    ValueTask<BuildRunArtifactAccountingOperationResult> AccountArtifactsAsync (
        BuildRunArtifactAccountingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary> Writes <c>build.json</c> and returns its artifact reference. </summary>
    ValueTask<BuildArtifactRefWriteResult> WriteMetadataAsync (
        BuildRunMetadataWriteRequest request,
        CancellationToken cancellationToken = default);
}

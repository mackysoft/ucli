using MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Creates and writes build run artifacts under local uCLI storage. </summary>
internal interface IBuildRunArtifactStore
{
    /// <summary> Prepares artifact paths for one build run. </summary>
    ValueTask<BuildRunArtifactPrepareResult> PrepareAsync (
        ResolvedUnityProjectContext unityProject,
        string runId,
        CancellationToken cancellationToken = default);

    /// <summary> Writes the build output manifest by scanning the output directory. </summary>
    ValueTask<BuildOutputManifestResult> WriteOutputManifestAsync (
        BuildRunArtifactPaths paths,
        string target,
        CancellationToken cancellationToken = default);

    /// <summary> Writes the build metadata artifact. </summary>
    ValueTask<BuildArtifactWriteResult> WriteMetadataAsync (
        BuildRunArtifactPaths paths,
        BuildRunMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary> Calculates the digest for an existing artifact file. </summary>
    ValueTask<BuildArtifactWriteResult> CalculateDigestAsync (
        string path,
        CancellationToken cancellationToken = default);

    /// <summary> Calculates the digest for a required artifact file and maps a missing file to the supplied code. </summary>
    ValueTask<BuildArtifactWriteResult> CalculateRequiredDigestAsync (
        string path,
        UcliCode missingCode,
        CancellationToken cancellationToken = default);
}

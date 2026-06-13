namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents inputs needed to persist <c>build.json</c> after artifact accounting. </summary>
/// <param name="Paths"> The prepared artifact layout. </param>
/// <param name="Metadata"> The metadata document fields persisted into <c>build.json</c>. </param>
/// <param name="Accounting"> The non-metadata artifact accounting embedded into <c>build.json</c>. </param>
internal sealed record BuildRunMetadataWriteRequest (
    BuildRunArtifactPaths Paths,
    BuildRunMetadataDocument Metadata,
    BuildRunArtifactAccountingResult Accounting);

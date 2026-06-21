namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents inputs needed to account non-metadata build-run artifacts. </summary>
/// <param name="Paths"> The prepared artifact layout. </param>
/// <param name="BuildTarget"> The resolved buildTarget stable name. </param>
/// <param name="UnityBuildTarget"> The Unity <c>BuildTarget</c> enum member name. </param>
/// <param name="OutputSources"> The output source entries to ingest into the artifact store. </param>
/// <param name="AllowEmptyOutputManifest"> Whether no existing output sources may produce a valid empty manifest. </param>
internal sealed record BuildRunArtifactAccountingRequest (
    BuildRunArtifactPaths Paths,
    string BuildTarget,
    string UnityBuildTarget,
    IReadOnlyList<BuildOutputSourceEntry> OutputSources,
    bool AllowEmptyOutputManifest);

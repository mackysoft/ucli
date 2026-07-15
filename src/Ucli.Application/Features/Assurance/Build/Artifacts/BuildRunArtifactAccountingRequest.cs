using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents inputs needed to account non-metadata build-run artifacts. </summary>
/// <param name="Paths"> The prepared artifact layout. </param>
/// <param name="BuildTarget"> The resolved buildTarget stable name. </param>
/// <param name="UnityBuildTarget"> The Unity <c>BuildTarget</c> enum member name. </param>
/// <param name="BuildReport"> The BuildReport source to write and account, or <see langword="null" /> when no BuildReport evidence was produced. </param>
/// <param name="OutputSources"> The output source entries to ingest into the artifact store. </param>
/// <param name="AllowEmptyOutputManifest"> Whether no existing output sources may produce a valid empty manifest. </param>
internal sealed record BuildRunArtifactAccountingRequest (
    BuildRunArtifactPaths Paths,
    BuildTargetStableName BuildTarget,
    string UnityBuildTarget,
    BuildReportSourceEntry? BuildReport,
    IReadOnlyList<BuildOutputSourceEntry> OutputSources,
    bool AllowEmptyOutputManifest);

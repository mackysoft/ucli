namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents inputs needed to account non-metadata build-run artifacts. </summary>
/// <param name="Paths"> The prepared artifact layout. </param>
/// <param name="ReportedOutputPath"> The output path reported by Unity's normalized BuildReport. </param>
/// <param name="BuildTarget"> The resolved buildTarget stable name. </param>
internal sealed record BuildRunArtifactAccountingRequest (
    BuildRunArtifactPaths Paths,
    string ReportedOutputPath,
    string BuildTarget);

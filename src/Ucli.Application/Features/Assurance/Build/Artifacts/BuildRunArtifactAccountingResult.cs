namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents artifact references and accounting emitted before <c>build.json</c> is written. </summary>
/// <param name="BuildReport"> The accounted <c>build-report.json</c> artifact reference, or <see langword="null" /> when no BuildReport evidence was produced. </param>
/// <param name="BuildOutputManifest"> The persisted <c>output-manifest.json</c> artifact reference. </param>
/// <param name="BuildLog"> The accounted <c>build.log</c> artifact reference. </param>
/// <param name="OutputManifest"> The player output manifest content summary. </param>
internal sealed record BuildRunArtifactAccountingResult (
    BuildArtifactRef? BuildReport,
    BuildArtifactRef BuildOutputManifest,
    BuildArtifactRef BuildLog,
    BuildOutputManifestSummary OutputManifest);

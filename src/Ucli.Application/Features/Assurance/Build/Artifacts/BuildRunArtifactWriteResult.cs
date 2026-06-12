namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents artifact references and accounting emitted by one build artifact write. </summary>
/// <param name="Build"> The persisted <c>build.json</c> artifact reference. </param>
/// <param name="BuildReport"> The persisted <c>build-report.json</c> artifact reference. </param>
/// <param name="BuildOutputManifest"> The persisted <c>output-manifest.json</c> artifact reference. </param>
/// <param name="BuildLog"> The persisted <c>build.log</c> artifact reference. </param>
/// <param name="OutputManifest"> The player output manifest accounting summary. </param>
internal sealed record BuildRunArtifactWriteResult (
    BuildArtifactRef Build,
    BuildArtifactRef BuildReport,
    BuildArtifactRef BuildOutputManifest,
    BuildArtifactRef BuildLog,
    BuildOutputManifestSummary OutputManifest);

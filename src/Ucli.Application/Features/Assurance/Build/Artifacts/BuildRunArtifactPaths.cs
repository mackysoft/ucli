namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents the resolved filesystem layout for one build run. </summary>
/// <param name="RepositoryRoot"> The repository root used to resolve storage layout identity for this build run. </param>
/// <param name="RunId"> The build run identifier. </param>
/// <param name="ArtifactsDirectory"> The absolute build-run artifact directory path. </param>
/// <param name="BuildJsonPath"> The absolute <c>build.json</c> path. </param>
/// <param name="BuildReportJsonPath"> The absolute <c>build-report.json</c> path. </param>
/// <param name="BuildLogPath"> The absolute <c>build.log</c> path. </param>
/// <param name="OutputManifestJsonPath"> The absolute <c>output-manifest.json</c> path. </param>
/// <param name="RunnerOutputDirectory"> The absolute runner working output root path. </param>
/// <param name="ArtifactOutputDirectory"> The absolute artifact-store output root path. </param>
internal sealed record BuildRunArtifactPaths (
    string RepositoryRoot,
    string RunId,
    string ArtifactsDirectory,
    string BuildJsonPath,
    string BuildReportJsonPath,
    string BuildLogPath,
    string OutputManifestJsonPath,
    string RunnerOutputDirectory,
    string ArtifactOutputDirectory);

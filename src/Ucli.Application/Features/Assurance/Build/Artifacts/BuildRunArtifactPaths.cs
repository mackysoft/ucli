namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents absolute artifact paths for one build run. </summary>
internal sealed record BuildRunArtifactPaths (
    string RunDirectory,
    string BuildJsonPath,
    string BuildReportPath,
    string BuildLogPath,
    string OutputManifestPath,
    string OutputDirectory);

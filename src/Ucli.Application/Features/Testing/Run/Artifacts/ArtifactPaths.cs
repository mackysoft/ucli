namespace MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

/// <summary> Represents fixed artifact file paths for one test-run execution. </summary>
internal sealed record ArtifactPaths (
    string ArtifactsDir,
    string MetaJsonPath,
    string ResultsXmlPath,
    string EditorLogPath,
    string ResultsJsonPath,
    string SummaryJsonPath);

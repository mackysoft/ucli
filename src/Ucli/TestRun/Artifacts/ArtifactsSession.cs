namespace MackySoft.Ucli.TestRun.Artifacts;

/// <summary> Represents artifact-session metadata for one test-run execution. </summary>
/// <param name="RunId"> The generated run identifier. </param>
/// <param name="ArtifactsDir"> The absolute run artifacts directory path. </param>
/// <param name="Paths"> The fixed artifact file paths. </param>
/// <param name="StartedAtUtc"> The UTC start timestamp of the run. </param>
internal sealed record ArtifactsSession (
    string RunId,
    string ArtifactsDir,
    ArtifactPaths Paths,
    DateTimeOffset StartedAtUtc);
namespace MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;

/// <summary> Represents artifact-session metadata for one test-run execution. </summary>
/// <param name="RunId"> The generated run identifier. </param>
/// <param name="Paths"> The fixed artifact file paths. </param>
/// <param name="StartedAtUtc"> The UTC start timestamp of the run. </param>
internal sealed record ArtifactsSession (
    string RunId,
    ArtifactPaths Paths,
    DateTimeOffset StartedAtUtc);

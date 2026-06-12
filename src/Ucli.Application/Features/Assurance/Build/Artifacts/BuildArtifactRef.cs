namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents one persisted build-run artifact reference. </summary>
/// <param name="Key"> The stable artifact key used by callers and JSON object properties. </param>
/// <param name="Kind"> The semantic artifact kind. </param>
/// <param name="Path"> The repository-relative slash-separated artifact path. </param>
/// <param name="Sha256"> The lowercase SHA-256 digest for the artifact file content. </param>
internal sealed record BuildArtifactRef (
    string Key,
    BuildArtifactKind Kind,
    string Path,
    string Sha256);

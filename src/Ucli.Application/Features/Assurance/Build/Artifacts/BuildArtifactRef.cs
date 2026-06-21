namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents one persisted build-run artifact reference. </summary>
/// <param name="Kind"> The semantic artifact kind. </param>
/// <param name="Path"> The artifact-root relative slash-separated artifact path. </param>
/// <param name="Digest"> The lowercase SHA-256 digest for the artifact identity. </param>
internal sealed record BuildArtifactRef (
    BuildArtifactKind Kind,
    string Path,
    string Digest);

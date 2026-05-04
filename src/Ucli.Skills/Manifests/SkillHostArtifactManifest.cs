namespace MackySoft.Ucli.Skills.Manifests;

/// <summary> Represents one host-specific artifact digest entry in <c>ucli-skill.json</c>. </summary>
/// <param name="Host"> The canonical host literal. </param>
/// <param name="Path"> The host artifact path, or <see langword="null" /> when the artifact is frontmatter-only. </param>
/// <param name="Digest"> The host artifact digest, or <see langword="null" /> when no file artifact exists. </param>
/// <param name="MaterializedFrontmatterDigest"> The materialized SKILL.md frontmatter digest. </param>
public sealed record SkillHostArtifactManifest (
    string Host,
    string? Path,
    string? Digest,
    string MaterializedFrontmatterDigest);

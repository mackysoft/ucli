namespace MackySoft.Ucli.Skills.Hosts.Contracts;

/// <summary> Represents deterministic host-specific artifacts for one skill. </summary>
/// <param name="Frontmatter"> The materialized SKILL.md frontmatter including delimiters and trailing LF. </param>
/// <param name="MetadataContent"> The optional host metadata content. The path is declared by the host adapter. </param>
public sealed record SkillHostArtifactSet (
    string Frontmatter,
    string? MetadataContent);

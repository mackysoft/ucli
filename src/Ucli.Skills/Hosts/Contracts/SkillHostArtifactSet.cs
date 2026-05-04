using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Hosts.Contracts;

/// <summary> Represents deterministic host-specific artifacts for one skill. </summary>
/// <param name="Frontmatter"> The materialized SKILL.md frontmatter including delimiters and trailing LF. </param>
/// <param name="AdditionalFiles"> Additional host-specific files. </param>
public sealed record SkillHostArtifactSet (
    string Frontmatter,
    IReadOnlyList<SkillPackageFile> AdditionalFiles);

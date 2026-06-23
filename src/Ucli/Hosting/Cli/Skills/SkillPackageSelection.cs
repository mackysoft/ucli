using MackySoft.AgentSkills.Tiers;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents the normalized SKILL package selection supplied by CLI options. </summary>
/// <param name="Tiers"> The selected SKILL tiers. </param>
/// <param name="SkillNames"> The selected exact SKILL names. Empty means no name filter. </param>
internal sealed record SkillPackageSelection (
    IReadOnlyList<SkillTier> Tiers,
    IReadOnlyList<string> SkillNames);

using MackySoft.AgentSkills.Tiers;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary> Represents the normalized SKILL prune selection supplied by CLI options. </summary>
/// <param name="ReportTiers"> The tiers to report in the command payload. </param>
/// <param name="TierFilter"> The optional tier filter passed to the prune service. Empty means every tier. </param>
/// <param name="SkillNames"> The selected exact SKILL names. Empty means no name filter. </param>
internal sealed record SkillPruneSelection (
    IReadOnlyList<SkillTier> ReportTiers,
    IReadOnlyList<SkillTier> TierFilter,
    IReadOnlyList<string> SkillNames);

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents a completed SKILL install operation. </summary>
/// <param name="TargetRoot"> The canonical absolute target root. </param>
/// <param name="Actions"> The per-skill install actions. </param>
public sealed record SkillInstallResult (
    string TargetRoot,
    IReadOnlyList<SkillInstallAction> Actions);

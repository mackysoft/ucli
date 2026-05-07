namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents a completed SKILL uninstall operation. </summary>
/// <param name="TargetRoot"> The canonical absolute target root. </param>
/// <param name="Actions"> The per-skill uninstall actions. </param>
public sealed record SkillUninstallResult (
    string TargetRoot,
    IReadOnlyList<SkillUninstallAction> Actions);

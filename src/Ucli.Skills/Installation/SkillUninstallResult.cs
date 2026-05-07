namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents a planned or completed SKILL uninstall operation. </summary>
/// <param name="TargetRoot"> The canonical absolute target root. </param>
/// <param name="Actions"> The per-skill uninstall actions. </param>
/// <param name="DryRun"> Whether the result represents a plan without writes. </param>
/// <param name="Force"> Whether force delete was enabled. </param>
public sealed record SkillUninstallResult (
    string TargetRoot,
    IReadOnlyList<SkillUninstallAction> Actions,
    bool DryRun = false,
    bool Force = false);

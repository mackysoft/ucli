namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents a completed SKILL update operation. </summary>
/// <param name="TargetRoot"> The canonical absolute target root. </param>
/// <param name="Actions"> The per-skill update actions. </param>
public sealed record SkillUpdateResult (
    string TargetRoot,
    IReadOnlyList<SkillUpdateAction> Actions);

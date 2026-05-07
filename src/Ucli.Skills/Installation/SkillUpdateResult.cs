namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents a planned or completed SKILL update operation. </summary>
/// <param name="TargetRoot"> The canonical absolute target root. </param>
/// <param name="Actions"> The per-skill update actions. </param>
/// <param name="DryRun"> Whether the result represents a plan without writes. </param>
/// <param name="Force"> Whether force overwrite was enabled. </param>
/// <param name="PrintDiff"> Whether diff payloads were requested. </param>
public sealed record SkillUpdateResult (
    string TargetRoot,
    IReadOnlyList<SkillUpdateAction> Actions,
    bool DryRun = false,
    bool Force = false,
    bool PrintDiff = false);

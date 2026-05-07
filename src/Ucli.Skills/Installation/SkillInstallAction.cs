namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents one per-skill install action. </summary>
/// <param name="Identity"> The install identity. </param>
/// <param name="ActionKind"> The action kind. </param>
/// <param name="BlockedReason"> The blocked reason category, when the action is blocked. </param>
/// <param name="Diffs"> The optional structured diffs. </param>
public sealed record SkillInstallAction (
    SkillInstallIdentity Identity,
    SkillInstallActionKind ActionKind,
    SkillBlockedReason? BlockedReason = null,
    IReadOnlyList<SkillActionDiff>? Diffs = null);

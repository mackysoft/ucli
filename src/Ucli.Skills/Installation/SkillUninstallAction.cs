namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents one per-skill uninstall action. </summary>
/// <param name="Identity"> The install identity. </param>
/// <param name="ActionKind"> The action kind. </param>
/// <param name="BlockedReason"> The stable blocked reason literal, when the action is blocked. </param>
public sealed record SkillUninstallAction (
    SkillInstallIdentity Identity,
    SkillUninstallActionKind ActionKind,
    string? BlockedReason = null);

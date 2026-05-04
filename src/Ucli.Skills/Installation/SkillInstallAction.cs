namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents one per-skill install action. </summary>
/// <param name="Identity"> The install identity. </param>
/// <param name="ActionKind"> The action kind. </param>
public sealed record SkillInstallAction (
    SkillInstallIdentity Identity,
    SkillInstallActionKind ActionKind);

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents one per-skill update action. </summary>
/// <param name="Identity"> The install identity. </param>
/// <param name="ActionKind"> The action kind. </param>
public sealed record SkillUpdateAction (
    SkillInstallIdentity Identity,
    SkillUpdateActionKind ActionKind);

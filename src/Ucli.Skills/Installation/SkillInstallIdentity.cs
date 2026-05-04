namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Identifies one installed SKILL instance. </summary>
/// <param name="Host"> The host. </param>
/// <param name="Scope"> The install scope. </param>
/// <param name="TargetRoot"> The canonical absolute host target root. </param>
/// <param name="SkillName"> The skill name. </param>
public sealed record SkillInstallIdentity (
    string Host,
    SkillScopeKind Scope,
    string TargetRoot,
    string SkillName);

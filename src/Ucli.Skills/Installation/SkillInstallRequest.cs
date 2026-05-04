using MackySoft.Ucli.Skills.Hosts;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents one project-scope SKILL install request. </summary>
/// <param name="Host"> The target host. </param>
/// <param name="Scope"> The install scope. </param>
/// <param name="RepositoryRoot"> The repository root. </param>
/// <param name="TargetRoot"> The optional explicit target root. </param>
public sealed record SkillInstallRequest (
    SkillHostKind Host,
    SkillScopeKind Scope,
    string RepositoryRoot,
    string? TargetRoot = null);

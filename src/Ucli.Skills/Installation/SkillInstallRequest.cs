namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents one SKILL install target request. </summary>
/// <param name="Host"> The target host. </param>
/// <param name="Scope"> The install scope. </param>
/// <param name="RepositoryRoot"> The repository root required for project scope. </param>
/// <param name="TargetRoot"> The optional explicit target root. </param>
public sealed record SkillInstallRequest (
    string Host,
    SkillScopeKind Scope,
    string? RepositoryRoot,
    string? TargetRoot = null);

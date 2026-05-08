namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Defines supported SKILL install scopes. </summary>
public enum SkillScopeKind
{
    /// <summary> Project-local installation under a repository root. </summary>
    Project = 0,

    /// <summary> User-local installation under the target host's personal SKILL root. </summary>
    User = 1,
}

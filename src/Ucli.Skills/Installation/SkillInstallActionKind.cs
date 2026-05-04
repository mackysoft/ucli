namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Defines the outcome for one installed official SKILL. </summary>
public enum SkillInstallActionKind
{
    /// <summary> The target skill directory was created. </summary>
    Created = 0,

    /// <summary> The target skill directory already contained matching content for the same host. </summary>
    NoOp = 1,
}

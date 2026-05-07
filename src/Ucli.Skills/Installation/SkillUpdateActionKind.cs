namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Defines the outcome for one official SKILL update. </summary>
public enum SkillUpdateActionKind
{
    /// <summary> The target skill directory was created because it was missing. </summary>
    Created = 0,

    /// <summary> The target skill directory was replaced with the current canonical package. </summary>
    Updated = 1,

    /// <summary> The target skill directory already contained current content for the same host. </summary>
    NoOp = 2,
}

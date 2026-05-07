namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Defines the outcome for one official SKILL update. </summary>
public enum SkillUpdateActionKind
{
    /// <summary> The target skill directory is planned to be created or was created because it was missing. </summary>
    Created = 0,

    /// <summary> The target skill directory is planned to be replaced or was replaced with the current canonical package. </summary>
    Updated = 1,

    /// <summary> The target skill directory already contained current content for the same host. </summary>
    NoOp = 2,

    /// <summary> The target contains local modifications and force was not enabled. </summary>
    BlockedLocalModification = 3,

    /// <summary> The target is unmanaged and cannot be overwritten. </summary>
    BlockedUnmanaged = 4,
}

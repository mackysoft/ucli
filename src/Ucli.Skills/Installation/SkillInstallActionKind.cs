namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Defines the outcome for one installed official SKILL. </summary>
public enum SkillInstallActionKind
{
    /// <summary> The target skill directory is planned to be created or was created. </summary>
    Created = 0,

    /// <summary> The managed target skill directory is planned to be replaced or was replaced with the current canonical package. </summary>
    Updated = 1,

    /// <summary> The target skill directory already contained matching content for the same host. </summary>
    NoOp = 2,

    /// <summary> The target contains managed non-current content and force was not enabled. </summary>
    BlockedManagedOverwrite = 3,

    /// <summary> The target contains local modifications and force was not enabled. </summary>
    BlockedLocalModification = 4,

    /// <summary> The target is unmanaged and cannot be overwritten. </summary>
    BlockedUnmanaged = 5,
}

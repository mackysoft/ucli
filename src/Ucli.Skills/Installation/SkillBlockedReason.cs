namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Defines blocked action reason categories. </summary>
public enum SkillBlockedReason
{
    /// <summary> The operation would overwrite a managed target without <c>--force</c>. </summary>
    ManagedOverwriteRequiresForce = 0,

    /// <summary> The operation would overwrite or delete local modifications without <c>--force</c>. </summary>
    LocalModificationRequiresForce = 1,

    /// <summary> The target directory is not managed by uCLI. </summary>
    UnmanagedTarget = 2,
}

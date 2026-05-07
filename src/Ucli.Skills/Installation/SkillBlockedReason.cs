namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Defines blocked action reason literals. </summary>
public static class SkillBlockedReason
{
    /// <summary> The operation would overwrite a managed target without <c>--force</c>. </summary>
    public const string ManagedOverwriteRequiresForce = "managedOverwriteRequiresForce";

    /// <summary> The operation would overwrite or delete local modifications without <c>--force</c>. </summary>
    public const string LocalModificationRequiresForce = "localModificationRequiresForce";

    /// <summary> The target directory is not managed by uCLI. </summary>
    public const string UnmanagedTarget = "unmanagedTarget";
}

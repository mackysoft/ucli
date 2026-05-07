namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Defines the outcome for one official SKILL uninstall. </summary>
public enum SkillUninstallActionKind
{
    /// <summary> The managed target skill directory was deleted. </summary>
    Deleted = 0,

    /// <summary> The target skill directory was already absent. </summary>
    NoOp = 1,

    /// <summary> The target skill directory exists but is not managed by uCLI. </summary>
    SkippedUnmanaged = 2,
}

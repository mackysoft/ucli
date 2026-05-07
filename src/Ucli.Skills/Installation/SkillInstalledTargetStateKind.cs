namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Defines the state of one installed official SKILL target. </summary>
public enum SkillInstalledTargetStateKind
{
    /// <summary> The target skill directory is absent. </summary>
    Missing = 0,

    /// <summary> The target matches the current canonical package and requested host. </summary>
    Current = 1,

    /// <summary> The target is managed and clean, but does not match the current canonical package. </summary>
    CleanOutdated = 2,

    /// <summary> The target is managed but contains local modifications. </summary>
    LocalModified = 3,

    /// <summary> The target skill directory exists without a uCLI manifest. </summary>
    Unmanaged = 4,
}

using MackySoft.Ucli.Skills.Manifests;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents the analyzed state of one installed official SKILL target. </summary>
/// <param name="Kind"> The target state kind. </param>
/// <param name="InstalledManifest"> The installed manifest when the target is managed and valid. </param>
public sealed record SkillInstalledTargetState (
    SkillInstalledTargetStateKind Kind,
    SkillManifest? InstalledManifest = null);

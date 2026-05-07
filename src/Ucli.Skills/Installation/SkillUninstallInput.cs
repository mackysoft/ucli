using MackySoft.Ucli.Skills.Packaging;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents one SKILL uninstall service input. </summary>
/// <param name="Packages"> The canonical packages to remove. </param>
/// <param name="TargetRequest"> The host target request. </param>
public sealed record SkillUninstallInput (
    IReadOnlyList<CanonicalSkillPackage> Packages,
    SkillInstallRequest TargetRequest);

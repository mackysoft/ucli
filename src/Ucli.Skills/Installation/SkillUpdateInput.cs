using MackySoft.Ucli.Skills.Packaging;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents one SKILL update service input. </summary>
/// <param name="Packages"> The canonical packages to reconcile. </param>
/// <param name="TargetRequest"> The host target request. </param>
public sealed record SkillUpdateInput (
    IReadOnlyList<CanonicalSkillPackage> Packages,
    SkillInstallRequest TargetRequest);

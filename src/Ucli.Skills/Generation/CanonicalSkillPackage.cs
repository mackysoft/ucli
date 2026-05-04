using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Generation;

/// <summary> Represents one canonical host-independent SKILL package. </summary>
/// <param name="SkillName"> The skill name. </param>
/// <param name="DisplayName"> The display name. </param>
/// <param name="Description"> The skill description. </param>
/// <param name="Manifest"> The canonical manifest. </param>
/// <param name="Files"> The canonical package files. </param>
public sealed record CanonicalSkillPackage (
    string SkillName,
    string DisplayName,
    string Description,
    SkillManifest Manifest,
    IReadOnlyList<SkillPackageFile> Files);

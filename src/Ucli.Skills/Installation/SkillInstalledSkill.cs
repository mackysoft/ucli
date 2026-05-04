using MackySoft.Ucli.Skills.Manifests;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents one scanned installed SKILL manifest. </summary>
/// <param name="Identity"> The install identity. </param>
/// <param name="SkillDirectory"> The skill directory. </param>
/// <param name="Manifest"> The scanned manifest. </param>
public sealed record SkillInstalledSkill (
    SkillInstallIdentity Identity,
    string SkillDirectory,
    SkillManifest Manifest);

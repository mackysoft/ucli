namespace MackySoft.Ucli.Skills.Hosts.Contracts;

/// <summary> Describes one supported SKILL host adapter. </summary>
/// <param name="HostKey"> The canonical host key. </param>
/// <param name="ProjectTargetDirectory"> The project-scope target directory relative to repository root. </param>
/// <param name="UserTargetDirectory"> The user-scope target directory shown to users. </param>
/// <param name="UserTargetRootPolicy"> The user-scope target root resolution policy. </param>
/// <param name="ReloadGuidance"> The host-specific guidance for reloading installed SKILLs. </param>
public sealed record SkillHostDescriptor (
    string HostKey,
    string ProjectTargetDirectory,
    string UserTargetDirectory,
    SkillUserTargetRootPolicy UserTargetRootPolicy,
    string ReloadGuidance);

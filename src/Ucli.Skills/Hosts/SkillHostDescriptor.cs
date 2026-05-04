namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Describes one supported SKILL host adapter. </summary>
/// <param name="HostKey"> The canonical host key. </param>
/// <param name="ProjectTargetDirectory"> The project-scope target directory relative to repository root. </param>
public sealed record SkillHostDescriptor (
    string HostKey,
    string ProjectTargetDirectory);

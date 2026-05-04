namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Describes one supported SKILL host adapter. </summary>
/// <param name="Host"> The host enum value. </param>
/// <param name="HostName"> The canonical host literal. </param>
/// <param name="ProjectTargetDirectory"> The project-scope target directory relative to repository root. </param>
public sealed record SkillHostDescriptor (
    SkillHostKind Host,
    string HostName,
    string ProjectTargetDirectory);

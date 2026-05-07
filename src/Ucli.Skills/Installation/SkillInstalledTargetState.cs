namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents the analyzed state of one installed official SKILL target. </summary>
/// <param name="Kind"> The target state kind. </param>
public sealed record SkillInstalledTargetState (SkillInstalledTargetStateKind Kind);

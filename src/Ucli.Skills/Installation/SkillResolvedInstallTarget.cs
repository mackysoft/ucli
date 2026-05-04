namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents a resolved host install target. </summary>
/// <param name="Host"> The canonical host key. </param>
/// <param name="TargetRoot"> The canonical absolute host target root. </param>
public sealed record SkillResolvedInstallTarget (
    string Host,
    string TargetRoot);

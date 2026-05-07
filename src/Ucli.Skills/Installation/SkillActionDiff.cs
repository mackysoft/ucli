namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents one structured file diff payload for an install action. </summary>
/// <param name="Files"> The file-level differences. </param>
public sealed record SkillActionDiff (IReadOnlyList<SkillFileDiff> Files);

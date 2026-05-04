namespace MackySoft.Ucli.Skills.Digests;

/// <summary> Represents one normalized file used for SKILL digest input. </summary>
/// <param name="RelativePath"> The slash-separated relative path. </param>
/// <param name="Content"> The LF-normalized content. </param>
public sealed record SkillDigestInputFile (
    string RelativePath,
    string Content);

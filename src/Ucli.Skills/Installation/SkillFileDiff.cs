namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Represents one structured file difference. </summary>
/// <param name="RelativePath"> The slash-separated path relative to the skill directory. </param>
/// <param name="ChangeKind"> The change kind literal. </param>
/// <param name="BeforeContent"> The previous file content, or <see langword="null" /> for additions. </param>
/// <param name="AfterContent"> The next file content, or <see langword="null" /> for deletions. </param>
public sealed record SkillFileDiff (
    string RelativePath,
    string ChangeKind,
    string? BeforeContent,
    string? AfterContent);

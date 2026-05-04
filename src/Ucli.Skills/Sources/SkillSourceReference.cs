namespace MackySoft.Ucli.Skills.Sources;

/// <summary> Represents one source reference template. </summary>
/// <param name="FileName"> The generated reference file name. </param>
/// <param name="Template"> The source template text. </param>
public sealed record SkillSourceReference (
    string FileName,
    string Template);

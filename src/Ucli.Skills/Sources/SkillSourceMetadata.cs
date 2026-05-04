namespace MackySoft.Ucli.Skills.Sources;

/// <summary> Represents host-independent metadata from source <c>skill.json</c>. </summary>
/// <param name="SchemaVersion"> The source schema version. </param>
/// <param name="SkillName"> The skill name. </param>
/// <param name="DisplayName"> The display name. </param>
/// <param name="Description"> The skill description. </param>
/// <param name="References"> The reference file names. </param>
public sealed record SkillSourceMetadata (
    int SchemaVersion,
    string SkillName,
    string DisplayName,
    string Description,
    IReadOnlyList<string> References)
{
    /// <summary> Gets the current source metadata schema version. </summary>
    public const int CurrentSchemaVersion = 1;
}

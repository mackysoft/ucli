namespace MackySoft.Ucli.Skills.Sources;

/// <summary> Represents one complete source SKILL definition. </summary>
/// <param name="Metadata"> The source metadata. </param>
/// <param name="SkillTemplate"> The source <c>SKILL.md.template</c> content. </param>
/// <param name="References"> The source reference templates. </param>
public sealed record SkillSourceDefinition (
    SkillSourceMetadata Metadata,
    string SkillTemplate,
    IReadOnlyList<SkillSourceReference> References);

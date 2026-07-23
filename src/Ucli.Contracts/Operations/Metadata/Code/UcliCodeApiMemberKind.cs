
namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Identifies the shape of a source-facing API member. </summary>
[VocabularyDefinition]
public enum UcliCodeApiMemberKind
{
    /// <summary> Indicates a property member. </summary>
    [VocabularyText("property")]
    Property = 1,

    /// <summary> Indicates a method member. </summary>
    [VocabularyText("method")]
    Method = 2,
}

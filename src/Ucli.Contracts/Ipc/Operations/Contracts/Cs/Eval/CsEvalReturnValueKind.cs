
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies how a C# eval return value is represented. </summary>
[VocabularyDefinition]
public enum CsEvalReturnValueKind
{
    /// <summary> Indicates that the evaluated entry point returned <see langword="null" />. </summary>
    [VocabularyText("null")]
    Null = 1,

    /// <summary> Indicates that the evaluated entry point returned a JSON value. </summary>
    [VocabularyText("json")]
    Json = 2,
}

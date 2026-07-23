
namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Identifies an accepted source form for an operation that compiles source code. </summary>
[VocabularyDefinition]
public enum UcliCodeSourceFormKind
{
    /// <summary> Indicates a complete C# compilation unit. </summary>
    [VocabularyText("compilationUnit")]
    CompilationUnit = 1,

    /// <summary> Indicates a generated entry-point method body. </summary>
    [VocabularyText("snippet")]
    Snippet = 2,
}

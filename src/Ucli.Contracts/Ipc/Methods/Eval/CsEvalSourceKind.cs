namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the explicit interpretation of C# evaluation source. </summary>
[VocabularyDefinition]
public enum CsEvalSourceKind
{
    /// <summary> Treats source as a Run method body. </summary>
    [VocabularyText("snippet")]
    Snippet = 1,

    /// <summary> Treats source as a complete C# compilation unit. </summary>
    [VocabularyText("compilationUnit")]
    CompilationUnit = 2,
}

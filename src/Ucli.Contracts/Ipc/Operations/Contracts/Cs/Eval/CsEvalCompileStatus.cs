
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the result of compiling C# eval source. </summary>
[VocabularyDefinition]
public enum CsEvalCompileStatus
{
    /// <summary> Indicates that compilation and entry-point validation succeeded. </summary>
    [VocabularyText("succeeded")]
    Succeeded = 1,

    /// <summary> Indicates that compilation or entry-point validation failed. </summary>
    [VocabularyText("failed")]
    Failed = 2,
}

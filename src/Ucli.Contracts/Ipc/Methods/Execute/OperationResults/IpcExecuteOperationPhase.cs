
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the final phase reached by an <c>execute</c> operation. </summary>
[VocabularyDefinition]
public enum IpcExecuteOperationPhase
{
    /// <summary> Indicates the validation phase. </summary>
    [VocabularyText("validate")]
    Validate = 1,

    /// <summary> Indicates the planning phase. </summary>
    [VocabularyText("plan")]
    Plan = 2,

    /// <summary> Indicates the call phase. </summary>
    [VocabularyText("call")]
    Call = 3,

    /// <summary> Indicates a step that was skipped. </summary>
    [VocabularyText("skipped")]
    Skipped = 4,
}

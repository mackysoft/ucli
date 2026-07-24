
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies whether a requested operation is known to have been applied. </summary>
[VocabularyDefinition]
public enum IpcApplicationState
{
    /// <summary> Indicates that the operation is known not to have been applied. </summary>
    [VocabularyText("notApplied")]
    NotApplied = 1,

    /// <summary> Indicates that the operation is known to have been applied. </summary>
    [VocabularyText("applied")]
    Applied = 2,

    /// <summary> Indicates that reliable evidence cannot determine whether the operation was applied. </summary>
    [VocabularyText("indeterminate")]
    Indeterminate = 3,

    /// <summary> Indicates that no reliable result envelope exists for the operation. </summary>
    [VocabularyText("unknown")]
    Unknown = 4,
}

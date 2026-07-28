namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Defines the step kinds supported by <c>execute</c> request arguments. </summary>
[VocabularyDefinition]
internal enum IpcExecuteStepKind
{
    [VocabularyText("op")]
    Op = 0,

    [VocabularyText("edit")]
    Edit,
}

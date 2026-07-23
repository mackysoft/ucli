
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines stable project mutation audit coverage literals. </summary>
[VocabularyDefinition]
public enum IpcBuildProjectMutationAuditCoverage
{
    /// <summary> All configured project mutation roots were audited. </summary>
    [VocabularyText("full")]
    Full = 1,

    /// <summary> Some configured project mutation roots were audited, but not all entries were covered. </summary>
    [VocabularyText("partial")]
    Partial = 2,

    /// <summary> The project mutation audit could not produce reliable evidence. </summary>
    [VocabularyText("indeterminate")]
    Indeterminate = 3,
}


namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines normalized Unity BuildReport result literals. </summary>
[VocabularyDefinition]
public enum IpcBuildReportResult
{
    /// <summary> The BuildReport indicated success. </summary>
    [VocabularyText("succeeded")]
    Succeeded = 1,

    /// <summary> The BuildReport indicated failure. </summary>
    [VocabularyText("failed")]
    Failed = 2,

    /// <summary> The BuildReport indicated cancellation. </summary>
    [VocabularyText("canceled")]
    Canceled = 3,

    /// <summary> The BuildReport result could not be classified. </summary>
    [VocabularyText("unknown")]
    Unknown = 4,
}

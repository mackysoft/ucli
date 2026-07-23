
namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Identifies the component that recorded a daemon diagnosis. </summary>
[VocabularyDefinition]
public enum DaemonDiagnosisReportedBy
{
    /// <summary> The Unity runtime recorded the diagnosis. </summary>
    [VocabularyText("unity")]
    Unity = 1,

    /// <summary> The uCLI process recorded or inferred the diagnosis. </summary>
    [VocabularyText("cli")]
    Cli = 2,
}

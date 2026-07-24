
namespace MackySoft.Ucli.Contracts;

/// <summary> Defines the closed set of top-level CLI command result outcomes. </summary>
[VocabularyDefinition]
public enum CommandResultStatus
{
    /// <summary> Indicates that the command completed successfully. </summary>
    [VocabularyText("ok")]
    Ok = 1,

    /// <summary> Indicates that the command failed. </summary>
    [VocabularyText("error")]
    Error = 2,
}

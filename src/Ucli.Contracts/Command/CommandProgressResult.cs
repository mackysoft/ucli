
namespace MackySoft.Ucli.Contracts;

/// <summary> Defines canonical result literals used by command progress entries. </summary>
[VocabularyDefinition]
public enum CommandProgressResult
{
    /// <summary> The observed progress step succeeded. </summary>
    [VocabularyText("succeeded")]
    Succeeded = 0,

    /// <summary> The observed progress step failed. </summary>
    [VocabularyText("failed")]
    Failed = 1,
}

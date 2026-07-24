
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the finite Unity execution modes accepted by assurance commands. </summary>
[VocabularyDefinition]
public enum AssuranceRequestedExecutionMode
{
    /// <summary> Selects a reusable daemon when available and otherwise launches a one-shot Unity process. </summary>
    [VocabularyText("auto")]
    Auto = 1,

    /// <summary> Requires execution through a reusable Unity daemon. </summary>
    [VocabularyText("daemon")]
    Daemon = 2,

    /// <summary> Requires execution through a one-shot Unity process. </summary>
    [VocabularyText("oneshot")]
    Oneshot = 3,
}

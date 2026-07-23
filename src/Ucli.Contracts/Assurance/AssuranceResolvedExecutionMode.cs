
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the finite Unity execution modes resolved by assurance commands. </summary>
[VocabularyDefinition]
public enum AssuranceResolvedExecutionMode
{
    /// <summary> The command executed through a reusable Unity daemon. </summary>
    [VocabularyText("daemon")]
    Daemon = 1,

    /// <summary> The command executed through a one-shot Unity process. </summary>
    [VocabularyText("oneshot")]
    Oneshot = 2,

    /// <summary> The command did not require a live Unity process. </summary>
    [VocabularyText("notApplicable")]
    NotApplicable = 3,
}

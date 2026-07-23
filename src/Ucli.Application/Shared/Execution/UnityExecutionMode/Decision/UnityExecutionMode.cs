namespace MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

/// <summary> Defines the requested Unity execution mode from the <c>--mode</c> option contract. </summary>
[VocabularyDefinition]
internal enum UnityExecutionMode
{
    /// <summary> Uses daemon when reachable; otherwise falls back to oneshot. </summary>
    [VocabularyText("auto")]
    Auto = 0,

    /// <summary> Requires daemon execution and rejects requests when daemon is not reachable. </summary>
    [VocabularyText("daemon")]
    Daemon = 1,

    /// <summary> Requires oneshot execution and rejects requests while daemon is reachable. </summary>
    [VocabularyText("oneshot")]
    Oneshot = 2,
}

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;

/// <summary> Identifies the outcome of clearing the visible Unity Console. </summary>
[VocabularyDefinition]
internal enum LogsUnityClearStatus
{
    /// <summary> The visible Unity Console was cleared. </summary>
    [VocabularyText("cleared")]
    Cleared = 0,
}

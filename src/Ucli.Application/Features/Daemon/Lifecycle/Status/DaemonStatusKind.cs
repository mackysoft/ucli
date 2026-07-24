
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

/// <summary> Defines daemon status query outcomes. </summary>
[VocabularyDefinition]
internal enum DaemonStatusKind
{
    [VocabularyText("running")]
    Running = 0,

    [VocabularyText("notRunning")]
    NotRunning = 1,

    [VocabularyText("stale")]
    Stale = 2,
}

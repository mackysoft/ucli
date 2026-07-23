
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;

/// <summary> Defines daemon stop operation outcomes. </summary>
[VocabularyDefinition]
internal enum DaemonStopStatus
{
    [VocabularyText("stopped")]
    Stopped = 0,

    [VocabularyText("notRunning")]
    NotRunning = 1,
}

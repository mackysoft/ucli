
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;

/// <summary> Defines daemon start operation outcomes. </summary>
[VocabularyDefinition]
internal enum DaemonStartStatus
{
    [VocabularyText("started")]
    Started = 0,

    [VocabularyText("alreadyRunning")]
    AlreadyRunning = 1,

    [VocabularyText("failed")]
    Failed = 2,

    [VocabularyText("attached")]
    Attached = 3,
}

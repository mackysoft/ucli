
namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Identifies one daemon-list item observation state. </summary>
[VocabularyDefinition]
internal enum DaemonListItemState
{
    [VocabularyText("running")]
    Running,

    [VocabularyText("stale")]
    Stale,

    [VocabularyText("error")]
    Error,
}

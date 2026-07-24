
namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Identifies why one daemon-list item is not reported as running. </summary>
[VocabularyDefinition]
internal enum DaemonListItemReason
{
    [VocabularyText("staleSession")]
    StaleSession,

    [VocabularyText("invalidSession")]
    InvalidSession,

    [VocabularyText("probeTimeout")]
    ProbeTimeout,

    [VocabularyText("probeFailed")]
    ProbeFailed,
}

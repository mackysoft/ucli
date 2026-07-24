
namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Identifies why daemon-list output is partial. </summary>
[VocabularyDefinition]
internal enum DaemonListCompletionReason
{
    [VocabularyText("timeout")]
    Timeout,
}

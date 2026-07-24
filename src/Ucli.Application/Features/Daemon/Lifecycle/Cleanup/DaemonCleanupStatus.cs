
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Defines daemon cleanup outcome states. </summary>
[VocabularyDefinition]
internal enum DaemonCleanupStatus
{
    /// <summary> Indicates cleanup completed successfully. </summary>
    [VocabularyText("completed")]
    Completed = 0,

    /// <summary> Indicates cleanup was intentionally skipped for safety. </summary>
    [VocabularyText("skipped")]
    Skipped = 1,
}

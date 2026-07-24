
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Defines daemon cleanup skip reasons. </summary>
[VocabularyDefinition]
internal enum DaemonCleanupSkipReason
{
    /// <summary> Indicates cleanup was skipped because daemon is running. </summary>
    [VocabularyText("running")]
    Running = 1,

    /// <summary> Indicates cleanup was skipped because invalid session may still belong to a live daemon. </summary>
    [VocabularyText("unsafeInvalidSession")]
    UnsafeInvalidSession = 2,

    /// <summary> Indicates cleanup was skipped because reachability could not be determined safely. </summary>
    [VocabularyText("uncertainReachability")]
    UncertainReachability = 3,
}

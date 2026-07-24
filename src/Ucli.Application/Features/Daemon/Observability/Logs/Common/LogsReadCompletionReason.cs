
namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;

/// <summary> Identifies how a log-read operation completed. </summary>
[VocabularyDefinition]
internal enum LogsReadCompletionReason
{
    /// <summary> The bounded read or stream completed normally. </summary>
    [VocabularyText("completed")]
    Completed = 1,

    /// <summary> The stream stopped after its idle timeout elapsed. </summary>
    [VocabularyText("idleTimeout")]
    IdleTimeout = 2,

    /// <summary> The stream reached its inclusive upper timestamp bound. </summary>
    [VocabularyText("untilReached")]
    UntilReached = 3,

    /// <summary> The caller canceled the read. </summary>
    [VocabularyText("canceled")]
    Canceled = 4,

    /// <summary> The read failed. </summary>
    [VocabularyText("error")]
    Error = 5,
}

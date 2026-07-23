
namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines daemon startup retry-disposition literals. </summary>
[VocabularyDefinition]
public enum DaemonStartupRetryDisposition
{
    /// <summary> Caller may retry immediately. </summary>
    [VocabularyText("retryImmediately")]
    RetryImmediately = 0,

    /// <summary> Caller should wait before retrying. </summary>
    [VocabularyText("waitThenRetry")]
    WaitThenRetry = 1,

    /// <summary> Caller may retry after fixing the reported blocker. </summary>
    [VocabularyText("retryAfterFix")]
    RetryAfterFix = 2,

    /// <summary> Caller must perform manual action before retrying. </summary>
    [VocabularyText("manualActionRequired")]
    ManualActionRequired = 3,

    /// <summary> Caller should not retry this startup attempt. </summary>
    [VocabularyText("doNotRetry")]
    DoNotRetry = 4,

    /// <summary> Retry disposition is unknown. </summary>
    [VocabularyText("unknown")]
    Unknown = 5,
}

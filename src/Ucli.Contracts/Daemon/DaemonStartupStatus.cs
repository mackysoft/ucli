
namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines daemon startup observation status literals. </summary>
[VocabularyDefinition]
public enum DaemonStartupStatus
{
    /// <summary> Unity launch has started. </summary>
    [VocabularyText("launching")]
    Launching = 0,

    /// <summary> Startup is waiting for endpoint registration. </summary>
    [VocabularyText("waitingForEndpoint")]
    WaitingForEndpoint = 1,

    /// <summary> Startup is blocked by a classified condition. </summary>
    [VocabularyText("blocked")]
    Blocked = 2,

    /// <summary> Startup timed out before endpoint registration. </summary>
    [VocabularyText("timeout")]
    Timeout = 3,

    /// <summary> Startup failed before endpoint registration. </summary>
    [VocabularyText("failed")]
    Failed = 4,

    /// <summary> Startup completed endpoint registration. </summary>
    [VocabularyText("completed")]
    Completed = 5,
}

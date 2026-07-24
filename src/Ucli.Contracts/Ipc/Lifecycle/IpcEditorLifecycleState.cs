
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Editor lifecycle states exposed by lifecycle-bearing IPC contracts. </summary>
[VocabularyDefinition]
public enum IpcEditorLifecycleState
{
    /// <summary> Editor startup work is still running. </summary>
    [VocabularyText("starting")]
    Starting = 0,

    /// <summary> The daemon endpoint is recovering after an Editor lifecycle transition. </summary>
    [VocabularyText("recovering")]
    Recovering = 1,

    /// <summary> The Editor accepts ordinary execution requests. </summary>
    [VocabularyText("ready")]
    Ready = 2,

    /// <summary> Editor-internal work blocks ordinary execution requests. </summary>
    [VocabularyText("busy")]
    Busy = 3,

    /// <summary> Script compilation is active. </summary>
    [VocabularyText("compiling")]
    Compiling = 4,

    /// <summary> The most recently completed script compilation failed. </summary>
    [VocabularyText("compileFailed")]
    CompileFailed = 5,

    /// <summary> An AppDomain reload is active. </summary>
    [VocabularyText("domainReloading")]
    DomainReloading = 6,

    /// <summary> Asset refresh or reimport work is active. </summary>
    [VocabularyText("reimporting")]
    Reimporting = 7,

    /// <summary> Play Mode blocks ordinary execution requests. </summary>
    [VocabularyText("playmode")]
    PlayMode = 8,

    /// <summary> A modal Editor dialog blocks execution. </summary>
    [VocabularyText("modalBlocked")]
    ModalBlocked = 9,

    /// <summary> Unity Safe Mode blocks ordinary execution requests. </summary>
    [VocabularyText("safeMode")]
    SafeMode = 10,

    /// <summary> Editor shutdown is in progress. </summary>
    [VocabularyText("shuttingDown")]
    ShuttingDown = 11,

    /// <summary> The lifecycle cannot be observed because the daemon endpoint is unavailable. </summary>
    [VocabularyText("unavailable")]
    Unavailable = 12,
}

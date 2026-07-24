
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines reasons an Editor lifecycle state blocks ordinary execution requests. </summary>
[VocabularyDefinition]
public enum IpcEditorBlockingReason
{
    /// <summary> Editor startup work blocks execution. </summary>
    [VocabularyText("startup")]
    Startup = 0,

    /// <summary> Editor-internal work blocks execution. </summary>
    [VocabularyText("busy")]
    Busy = 1,

    /// <summary> Daemon endpoint recovery blocks execution. </summary>
    [VocabularyText("recovery")]
    Recovery = 2,

    /// <summary> Active script compilation blocks execution. </summary>
    [VocabularyText("compile")]
    Compile = 3,

    /// <summary> A failed script compilation blocks execution. </summary>
    [VocabularyText("compileFailed")]
    CompileFailed = 4,

    /// <summary> An AppDomain reload blocks execution. </summary>
    [VocabularyText("domainReload")]
    DomainReload = 5,

    /// <summary> Asset refresh or reimport work blocks execution. </summary>
    [VocabularyText("reimport")]
    Reimport = 6,

    /// <summary> Play Mode blocks ordinary execution requests. </summary>
    [VocabularyText("playMode")]
    PlayMode = 7,

    /// <summary> A modal Editor dialog blocks execution. </summary>
    [VocabularyText("modalDialog")]
    ModalDialog = 8,

    /// <summary> Unity Safe Mode blocks ordinary execution requests. </summary>
    [VocabularyText("safeMode")]
    SafeMode = 9,

    /// <summary> Editor shutdown blocks execution. </summary>
    [VocabularyText("shutdown")]
    Shutdown = 10,

    /// <summary> The lifecycle cannot be observed because the daemon endpoint is unavailable. </summary>
    [VocabularyText("unavailable")]
    Unavailable = 11,
}

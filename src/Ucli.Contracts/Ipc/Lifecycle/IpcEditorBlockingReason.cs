using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines reasons an Editor lifecycle state blocks ordinary execution requests. </summary>
public enum IpcEditorBlockingReason
{
    /// <summary> Editor startup work blocks execution. </summary>
    [UcliContractLiteral("startup")]
    Startup = 0,

    /// <summary> Editor-internal work blocks execution. </summary>
    [UcliContractLiteral("busy")]
    Busy = 1,

    /// <summary> Daemon endpoint recovery blocks execution. </summary>
    [UcliContractLiteral("recovery")]
    Recovery = 2,

    /// <summary> Active script compilation blocks execution. </summary>
    [UcliContractLiteral("compile")]
    Compile = 3,

    /// <summary> A failed script compilation blocks execution. </summary>
    [UcliContractLiteral("compileFailed")]
    CompileFailed = 4,

    /// <summary> An AppDomain reload blocks execution. </summary>
    [UcliContractLiteral("domainReload")]
    DomainReload = 5,

    /// <summary> Asset refresh or reimport work blocks execution. </summary>
    [UcliContractLiteral("reimport")]
    Reimport = 6,

    /// <summary> Play Mode blocks ordinary execution requests. </summary>
    [UcliContractLiteral("playMode")]
    PlayMode = 7,

    /// <summary> A modal Editor dialog blocks execution. </summary>
    [UcliContractLiteral("modalDialog")]
    ModalDialog = 8,

    /// <summary> Unity Safe Mode blocks ordinary execution requests. </summary>
    [UcliContractLiteral("safeMode")]
    SafeMode = 9,

    /// <summary> Editor shutdown blocks execution. </summary>
    [UcliContractLiteral("shutdown")]
    Shutdown = 10,

    /// <summary> The lifecycle cannot be observed because the daemon endpoint is unavailable. </summary>
    [UcliContractLiteral("unavailable")]
    Unavailable = 11,
}

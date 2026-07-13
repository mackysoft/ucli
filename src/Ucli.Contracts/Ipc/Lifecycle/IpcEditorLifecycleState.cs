using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Editor lifecycle states exposed by lifecycle-bearing IPC contracts. </summary>
public enum IpcEditorLifecycleState
{
    /// <summary> Editor startup work is still running. </summary>
    [UcliContractLiteral("starting")]
    Starting = 0,

    /// <summary> The daemon endpoint is recovering after an Editor lifecycle transition. </summary>
    [UcliContractLiteral("recovering")]
    Recovering = 1,

    /// <summary> The Editor accepts ordinary execution requests. </summary>
    [UcliContractLiteral("ready")]
    Ready = 2,

    /// <summary> Editor-internal work blocks ordinary execution requests. </summary>
    [UcliContractLiteral("busy")]
    Busy = 3,

    /// <summary> Script compilation is active. </summary>
    [UcliContractLiteral("compiling")]
    Compiling = 4,

    /// <summary> The most recently completed script compilation failed. </summary>
    [UcliContractLiteral("compileFailed")]
    CompileFailed = 5,

    /// <summary> An AppDomain reload is active. </summary>
    [UcliContractLiteral("domainReloading")]
    DomainReloading = 6,

    /// <summary> Asset refresh or reimport work is active. </summary>
    [UcliContractLiteral("reimporting")]
    Reimporting = 7,

    /// <summary> Play Mode blocks ordinary execution requests. </summary>
    [UcliContractLiteral("playmode")]
    PlayMode = 8,

    /// <summary> A modal Editor dialog blocks execution. </summary>
    [UcliContractLiteral("modalBlocked")]
    ModalBlocked = 9,

    /// <summary> Unity Safe Mode blocks ordinary execution requests. </summary>
    [UcliContractLiteral("safeMode")]
    SafeMode = 10,

    /// <summary> Editor shutdown is in progress. </summary>
    [UcliContractLiteral("shuttingDown")]
    ShuttingDown = 11,

    /// <summary> The lifecycle cannot be observed because the daemon endpoint is unavailable. </summary>
    [UcliContractLiteral("unavailable")]
    Unavailable = 12,
}

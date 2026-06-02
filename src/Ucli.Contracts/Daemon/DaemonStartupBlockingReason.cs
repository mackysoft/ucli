using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines daemon startup-blocking reason literals observed before endpoint registration. </summary>
public enum DaemonStartupBlockingReason
{
    /// <summary> Unity Editor safe mode blocked startup. </summary>
    [UcliContractLiteral("safeMode")]
    SafeMode = 0,

    /// <summary> Unity script compilation blocked startup. </summary>
    [UcliContractLiteral("compile")]
    Compile = 1,

    /// <summary> Unity package resolution blocked startup. </summary>
    [UcliContractLiteral("packageResolution")]
    PackageResolution = 2,

    /// <summary> The uCLI Unity plugin blocked startup. </summary>
    [UcliContractLiteral("ucliPlugin")]
    UcliPlugin = 3,

    /// <summary> A precompiled assembly conflict blocked startup. </summary>
    [UcliContractLiteral("precompiledAssemblyConflict")]
    PrecompiledAssemblyConflict = 4,

    /// <summary> A Unity modal dialog blocked startup. </summary>
    [UcliContractLiteral("modalDialog")]
    ModalDialog = 5,

    /// <summary> Endpoint registration did not complete. </summary>
    [UcliContractLiteral("endpointNotRegistered")]
    EndpointNotRegistered = 6,

    /// <summary> The Unity process exited before startup completed. </summary>
    [UcliContractLiteral("processExit")]
    ProcessExit = 7,

    /// <summary> Startup was blocked by an unknown condition. </summary>
    [UcliContractLiteral("unknown")]
    Unknown = 8,
}

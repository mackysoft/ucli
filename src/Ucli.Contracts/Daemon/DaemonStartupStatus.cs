using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines daemon startup observation status literals. </summary>
public enum DaemonStartupStatus
{
    /// <summary> Unity launch has started. </summary>
    [UcliContractLiteral("launching")]
    Launching = 0,

    /// <summary> Startup is waiting for endpoint registration. </summary>
    [UcliContractLiteral("waitingForEndpoint")]
    WaitingForEndpoint = 1,

    /// <summary> Startup is blocked by a classified condition. </summary>
    [UcliContractLiteral("blocked")]
    Blocked = 2,

    /// <summary> Startup timed out before endpoint registration. </summary>
    [UcliContractLiteral("timeout")]
    Timeout = 3,

    /// <summary> Startup failed before endpoint registration. </summary>
    [UcliContractLiteral("failed")]
    Failed = 4,

    /// <summary> Startup completed endpoint registration. </summary>
    [UcliContractLiteral("completed")]
    Completed = 5,
}

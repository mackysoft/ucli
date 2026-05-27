using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines process policies applied when daemon startup is blocked before endpoint registration. </summary>
public enum DaemonStartupBlockedProcessPolicy
{
    /// <summary> Uses the startup policy default for the current process ownership and Editor mode. </summary>
    [UcliContractLiteral("auto")]
    Auto = 0,

    /// <summary> Leaves the Unity process running after a startup blocker is detected. </summary>
    [UcliContractLiteral("keep")]
    Keep = 1,

    /// <summary> Terminates the Unity process when uCLI is allowed to manage that process. </summary>
    [UcliContractLiteral("terminate")]
    Terminate = 2,
}

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines daemon startup process-action literals. </summary>
public enum DaemonStartupProcessAction
{
    /// <summary> No Unity process action was required. </summary>
    [UcliContractLiteral("none")]
    None = 0,

    /// <summary> The Unity process was preserved. </summary>
    [UcliContractLiteral("kept")]
    Kept = 1,

    /// <summary> The Unity process was terminated. </summary>
    [UcliContractLiteral("terminated")]
    Terminated = 2,

    /// <summary> The process action outcome is unknown. </summary>
    [UcliContractLiteral("unknown")]
    Unknown = 3,
}

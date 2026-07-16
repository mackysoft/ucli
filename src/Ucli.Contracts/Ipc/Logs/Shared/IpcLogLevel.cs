using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines daemon and Unity log level literals. </summary>
public enum IpcLogLevel
{
    /// <summary> Error log level. </summary>
    [UcliContractLiteral("error")]
    Error = 1,

    /// <summary> Warning log level. </summary>
    [UcliContractLiteral("warning")]
    Warning = 2,

    /// <summary> Informational log level. </summary>
    [UcliContractLiteral("info")]
    Info = 3,
}

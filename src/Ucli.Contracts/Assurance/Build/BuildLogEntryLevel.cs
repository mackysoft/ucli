using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines <c>build.log.entry</c> level literals. </summary>
public enum BuildLogEntryLevel
{
    /// <summary> Trace log level. </summary>
    [UcliContractLiteral("trace")]
    Trace = 0,

    /// <summary> Debug log level. </summary>
    [UcliContractLiteral("debug")]
    Debug = 1,

    /// <summary> Informational log level. </summary>
    [UcliContractLiteral("info")]
    Info = 2,

    /// <summary> Warning log level. </summary>
    [UcliContractLiteral("warning")]
    Warning = 3,

    /// <summary> Error log level. </summary>
    [UcliContractLiteral("error")]
    Error = 4,
}

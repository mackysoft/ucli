using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines <c>build.log.entry</c> level literals. </summary>
public enum BuildLogEntryLevel
{
    /// <summary> Trace log level. </summary>
    [UcliContractLiteral("trace")]
    Trace = 1,

    /// <summary> Debug log level. </summary>
    [UcliContractLiteral("debug")]
    Debug = 2,

    /// <summary> Informational log level. </summary>
    [UcliContractLiteral("info")]
    Info = 3,

    /// <summary> Warning log level. </summary>
    [UcliContractLiteral("warning")]
    Warning = 4,

    /// <summary> Error log level. </summary>
    [UcliContractLiteral("error")]
    Error = 5,
}

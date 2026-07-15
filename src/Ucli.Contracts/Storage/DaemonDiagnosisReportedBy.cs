using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Identifies the component that recorded a daemon diagnosis. </summary>
public enum DaemonDiagnosisReportedBy
{
    /// <summary> The Unity runtime recorded the diagnosis. </summary>
    [UcliContractLiteral("unity")]
    Unity = 1,

    /// <summary> The uCLI process recorded or inferred the diagnosis. </summary>
    [UcliContractLiteral("cli")]
    Cli = 2,
}

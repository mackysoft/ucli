using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines <c>build.log.entry</c> source literals. </summary>
public enum BuildLogEntrySource
{
    /// <summary> Unity log stream entry source. </summary>
    [UcliContractLiteral("unityLog")]
    UnityLog = 1,

    /// <summary> Application-side ucli entry source. </summary>
    [UcliContractLiteral("ucli")]
    Ucli = 2,
}

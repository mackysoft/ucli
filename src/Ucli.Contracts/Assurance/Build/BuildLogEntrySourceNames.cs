using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the closed <c>build.log.entry</c> source set. </summary>
public static class BuildLogEntrySourceNames
{
    /// <summary> Gets the source for Unity log stream entries. </summary>
    public static string UnityLog => ContractLiteralCodec.ToValue(BuildLogEntrySource.UnityLog);

    /// <summary> Gets the source for application-side ucli entries. </summary>
    public static string Ucli => ContractLiteralCodec.ToValue(BuildLogEntrySource.Ucli);

    /// <summary> Gets the complete closed log source set. </summary>
    public static IReadOnlyList<string> All => ContractLiteralCodec.GetLiterals<BuildLogEntrySource>();
}

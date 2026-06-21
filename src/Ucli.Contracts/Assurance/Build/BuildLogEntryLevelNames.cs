using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the closed <c>build.log.entry</c> level set. </summary>
public static class BuildLogEntryLevelNames
{
    /// <summary> Gets the trace log level. </summary>
    public static string Trace => ContractLiteralCodec.ToValue(BuildLogEntryLevel.Trace);

    /// <summary> Gets the debug log level. </summary>
    public static string Debug => ContractLiteralCodec.ToValue(BuildLogEntryLevel.Debug);

    /// <summary> Gets the informational log level. </summary>
    public static string Info => ContractLiteralCodec.ToValue(BuildLogEntryLevel.Info);

    /// <summary> Gets the warning log level. </summary>
    public static string Warning => ContractLiteralCodec.ToValue(BuildLogEntryLevel.Warning);

    /// <summary> Gets the error log level. </summary>
    public static string Error => ContractLiteralCodec.ToValue(BuildLogEntryLevel.Error);

    /// <summary> Gets the complete closed log level set. </summary>
    public static IReadOnlyList<string> All => ContractLiteralCodec.GetLiterals<BuildLogEntryLevel>();
}
